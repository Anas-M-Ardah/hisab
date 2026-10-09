using System.IO;
namespace Hisab;
public static class AccountOrganizerTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(InvalidOperationException){results.Add("PASS "+name);return;}throw new Exception("Did not reject: "+name);}
        using var s=new Store(Path.Combine(directory,"organizer.db"));s.SaveAccount(null,"OG","Expenses","Expense");long root=s.AccountId("OG");s.SaveAccount(null,"OC","Water","Expense",root);long child=s.AccountId("OC");s.SaveTranslations("accounts",child,"المياه","Water bill","عنوان","Address");new AccountingService(s).Voucher(false,DateTime.Today,child,s.AccountId("1101"),12,"Water");
        long count=s.CommandCount;var snapshot=s.AccountOutline(true);Check(s.CommandCount-count==1,"Outline loads accounts, names and balances in one query");
        Check(snapshot.Single(x=>x.Account.Id==root).Total==12000&&snapshot.Single(x=>x.Account.Id==child).Own==12000,"Snapshot totals include descendants without duplicating own balance");
        Check(snapshot.Single(x=>x.Account.Id==child).DisplayName=="المياه"&&s.AccountOutline(false).Single(x=>x.Account.Id==child).DisplayName=="Water bill","Outline respects both name translations");
        count=s.CommandCount;Check(s.Accounts(true).Single(a=>a.Id==child).Name=="المياه"&&s.CommandCount-count==1,"Account pickers load localized names in one query");
        s.SaveItem(null,"OI","Sample item",true,2);long item=s.Items().Single(i=>i.Code=="OI").Id;s.SaveTranslations("items",item,"الصنف","Sample item");count=s.CommandCount;Check(s.Items(true).Single(i=>i.Id==item).Name=="الصنف"&&s.CommandCount-count==1,"Item lists load localized names in one query");
        var before=s.AccountEdit(child);s.EditAccount(child,"OC2","مياه المنزل",true,null);var after=s.AccountEdit(child);
        Check(after.Parent==null&&after.Code=="OC2"&&after.Arabic=="مياه المنزل","Organizer saves parent, code and active-language name");
        Check(after.Name==before.Name&&after.English==before.English&&(string)s.Table("SELECT address_en FROM accounts WHERE id=@p0",child).Rows[0][0]=="Address","Editor preserves original names, other translations and addresses");
        Check(s.Balance(child)==12000&&s.AccountOutline(true).Single(x=>x.Account.Id==root).Total==0,"Organizer edits preserve postings and update group balances");
        s.UndoAccountEdit(before,after);Check(s.AccountEdit(child)==before,"Undo restores exact names, code and parent");
        Reject(()=>s.EditAccount(root,"OG","Expenses",false,child),"Editor rejects hierarchy cycles");
        Reject(()=>s.EditAccount(child,"OC","Water",false,s.AccountId("1101")),"Editor rejects different account types");
        Reject(()=>s.EditAccount(child,"OC"," ",false,root),"Editor rejects an empty name");
        s.EditAccount(child,"OC2","Water updated",false,root);Reject(()=>s.UndoAccountEdit(before,after),"Undo refuses to overwrite a later account change");
        long core=s.AccountId("1101");Reject(()=>s.EditAccount(core,"NEW","Cash",false,null),"Organizer respects core account code protection");
        s.SaveUser("organizer-viewer","TestPassword123","Viewer");s.Login("organizer-viewer","TestPassword123");Reject(()=>s.EditAccount(child,"OC","Water",false,root),"Viewer cannot edit through the organizer");
    }
}

