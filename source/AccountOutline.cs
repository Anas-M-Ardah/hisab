using System.Data;
namespace Hisab;

public sealed record AccountOutlineEntry(Account Account,string DisplayName,long Own,long Total);
public sealed record AccountEditState(long Id,string Code,string Name,string Arabic,string English,long? Parent);

public sealed partial class Store
{
    // One read for the full tree, localized names and posted balances. UI operations
    // such as selection, searching and drag-over do not access the database.
    public List<AccountOutlineEntry> AccountOutline(bool arabic)
    {
        var rows=Table("SELECT a.*,coalesce(b.balance,0) balance FROM accounts a LEFT JOIN (SELECT account,sum(debit-credit) balance FROM entries GROUP BY account) b ON b.account=a.id ORDER BY a.code");
        var accounts=rows.Rows.Cast<DataRow>().Select(r=>new Account((long)r["id"],(string)r["code"],(string)r["name"],(string)r["kind"],(long)r["system"]==1,r["parent_id"]==DBNull.Value?null:(long)r["parent_id"])).ToDictionary(a=>a.Id);
        var own=rows.Rows.Cast<DataRow>().ToDictionary(r=>(long)r["id"],r=>(long)r["balance"]);
        var children=accounts.Values.Where(a=>a.ParentId!=null).ToLookup(a=>a.ParentId!.Value);
        var totals=new Dictionary<long,long>();var visiting=new HashSet<long>();
        long Total(long id){if(totals.TryGetValue(id,out var value))return value;if(!visiting.Add(id))throw new InvalidOperationException("Account hierarchy contains a cycle");value=own[id]+children[id].Sum(a=>Total(a.Id));visiting.Remove(id);return totals[id]=value;}
        return rows.Rows.Cast<DataRow>().Select(r=>{long id=(long)r["id"];string name=r[arabic?"name_ar":"name_en"]?.ToString()??"";return new AccountOutlineEntry(accounts[id],string.IsNullOrWhiteSpace(name)?accounts[id].Name:name,own[id],Total(id));}).ToList();
    }
    public AccountEditState AccountEdit(long id)
    {
        var row=Table("SELECT * FROM accounts WHERE id=@p0",id).Rows[0];return new(id,(string)row["code"],(string)row["name"],row["name_ar"]?.ToString()??"",row["name_en"]?.ToString()??"",row["parent_id"]==DBNull.Value?null:(long)row["parent_id"]);
    }
    public void EditAccount(long id,string code,string displayName,bool arabic,long? parent)
    {
        Require("master");if(string.IsNullOrWhiteSpace(displayName))throw new InvalidOperationException("اسم الحساب مطلوب / Account name is required");
        using var tx=BeginTransaction();var before=AccountEdit(id);var account=Accounts().Single(a=>a.Id==id);
        string current=arabic?before.Arabic:before.English;if(string.IsNullOrWhiteSpace(current))current=before.Name;
        SaveAccount(id,code,before.Name==current?displayName:before.Name,account.Kind,parent);
        Exec(arabic?"UPDATE accounts SET name_ar=@p0 WHERE id=@p1":"UPDATE accounts SET name_en=@p0 WHERE id=@p1",displayName.Trim(),id);tx.Commit();
    }
    public void UndoAccountEdit(AccountEditState before,AccountEditState after)
    {
        Require("master");using var tx=BeginTransaction();if(AccountEdit(after.Id)!=after)throw new InvalidOperationException("تغير الحساب بعد التعديل / Account changed since the edit");
        var account=Accounts().Single(a=>a.Id==before.Id);ValidateAccountParent(before.Id,account.Kind,before.Parent);
        Exec("UPDATE accounts SET code=@p0,name=@p1,name_ar=@p2,name_en=@p3,parent_id=@p4 WHERE id=@p5",before.Code,before.Name,before.Arabic,before.English,before.Parent,before.Id);Audit("account-undo",before.Id.ToString());tx.Commit();
    }
}
