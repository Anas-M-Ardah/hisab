using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using static Hisab.Store;
namespace Hisab;

// Accounting rules are separate from the WPF interface and SQLite persistence.
public sealed partial class AccountingService(Store store)
{
    SqliteConnection Db => store.Db;
    void Exec(string sql, params object?[] args) => store.Exec(sql,args);
    object? Scalar(string sql, params object?[] args) => store.Scalar(sql,args);
    DataTable Table(string sql, params object?[] args) => store.Table(sql,args);
    string Setting(string key) => store.Setting(key);
    List<Account> Accounts() => store.Accounts();
    List<Item> Items() => store.Items();
    long AccountId(string code) => store.AccountId(code);
    void Audit(string action,string detail) => Exec("INSERT INTO audit(action,detail,username) VALUES(@p0,@p1,@p2)",action,detail,store.User.Username);
    long NewDoc(string kind,DateTime date,long? party,long? cash,string note,Totals t,long paid=0,decimal vat=0,bool inclusive=false)
    {
        store.Require("post");EnsureOpen(date);
        if(date.Date>DateTime.Today) throw new InvalidOperationException("لا يمكن ترحيل حركة بتاريخ مستقبلي / Posting date cannot be in the future");
        int n=Convert.ToInt32(Scalar("SELECT count(*)+1 FROM documents WHERE kind=@p0",kind));
        var number=$"{kind}-{n:D6}";
        Exec("""
        INSERT INTO documents(number,kind,date,party,cash,note,net,tax,total,paid,vat,inclusive,company,terms,warranty,shipping,bank)
        VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16)
        """,number,kind,date.ToString("yyyy-MM-dd"),party,cash,note,t.Net,t.Tax,t.Total,paid,vat.ToString(CultureInfo.InvariantCulture),inclusive?1:0,Setting("company"),Setting("terms"),Setting("warranty"),Setting("shipping"),Setting("bank"));
        var id=Convert.ToInt64(Scalar("SELECT last_insert_rowid()")); Audit("post",number); return id;
    }
    void Journal(long doc,IEnumerable<EntryLine> input)
    {
        var lines=input.Where(x=>x.Debit!=0||x.Credit!=0).ToList();
        if(lines.Count<2 || lines.Any(x=>x.Debit<0||x.Credit<0||(x.Debit>0&&x.Credit>0))||lines.Sum(x=>x.Debit)==0||lines.Sum(x=>x.Debit)!=lines.Sum(x=>x.Credit)) throw new InvalidOperationException("القيد غير متوازن أو غير صالح / Journal must have equal positive debits and credits");
        foreach(var l in lines) {
            if(Accounts().Single(x=>x.Id==l.Account).Kind=="Unclassified") throw new InvalidOperationException("صنّف الحساب أولًا في دليل الحسابات / Classify the account first");
            Exec("INSERT INTO entries(doc,account,debit,credit) VALUES(@p0,@p1,@p2,@p3)",doc,l.Account,l.Debit,l.Credit);
        }
    }
    public long Voucher(bool receipt,DateTime date,long party,long cash,decimal amount,string note)
    {
        var value=M(amount); if(value<=0||D(value)!=amount) throw new InvalidOperationException("أدخل مبلغًا موجبًا بحد أقصى ٣ منازل عشرية / Enter a positive amount with at most 3 decimals");
        if(party==cash||Accounts().Single(x=>x.Id==cash).Kind!="Cash") throw new InvalidOperationException("اختر حساب صندوق أو بنك مختلفًا / Choose a separate cash or bank account");
        if(party==AccountId("1301"))throw new InvalidOperationException("استخدم الفواتير للمخزون / Use invoices for inventory");
        using var tx=store.BeginTransaction(); var id=NewDoc(receipt?"RC":"PV",date,party,cash,note,new(value,0,value));
        Journal(id,receipt?[new(cash,value,0),new(party,0,value)]:[new(party,value,0),new(cash,0,value)]); tx.Commit(); return id;
    }
    public long Manual(DateTime date,string note,List<EntryLine> lines)
    {
        if(lines.Any(x=>x.Account==AccountId("1301"))) throw new InvalidOperationException("سجّل المخزون من الفواتير أو الرصيد الافتتاحي / Use invoices or opening stock for inventory");
        using var tx=store.BeginTransaction(); var id=NewDoc("JV",date,null,null,note,new(lines.Sum(x=>x.Debit),0,lines.Sum(x=>x.Debit))); Journal(id,lines); tx.Commit(); return id;
    }
    public static Totals Calculate(IEnumerable<InvoiceLine> lines,decimal vat,bool inclusive)
    {
        if(vat<0||vat>100) throw new InvalidOperationException("نسبة الضريبة من 0 إلى 100 / Tax rate must be 0–100");
        long net=0,tax=0;
        foreach(var l in lines) {
            if(l.Qty<=0 || l.Price<0 || l.Discount<0 || l.Discount>l.Qty*l.Price || M(l.Qty)<=0 || D(M(l.Qty))!=l.Qty || D(M(l.Price))!=l.Price || D(M(l.Discount))!=l.Discount) throw new InvalidOperationException("تحقق من الكمية والسعر والخصم (٣ منازل عشرية كحد أقصى) / Check quantity, price and discount (max 3 decimals)");
            long gross=M(l.Qty*l.Price-l.Discount); long n=inclusive?M(D(gross)/(1+vat/100)):gross;
            long t=inclusive?gross-n:M(D(n)*vat/100); net=checked(net+n); tax=checked(tax+t);
        }
        return new(net,tax,checked(net+tax));
    }
    public long Invoice(bool sale,DateTime date,long party,long? cash,decimal paid,decimal vat,bool inclusive,string note,List<InvoiceLine> lines)
    {
        if(lines.Count==0) throw new InvalidOperationException("أضف صنفًا واحدًا على الأقل / Add at least one line");
        var t=Calculate(lines,vat,inclusive); long p=M(paid);
        if(t.Total<=0||paid<0||p>t.Total||D(p)!=paid) throw new InvalidOperationException("تحقق من إجمالي الفاتورة والمدفوع (٣ منازل عشرية كحد أقصى) / Check total and paid amount (max 3 decimals)");
        if(Accounts().Single(x=>x.Id==party).Kind!=(sale?"Customer":"Supplier")) throw new InvalidOperationException("اختر عميلًا أو موردًا مصنفًا / Choose a classified customer or supplier");
        if(p>0&&(cash==null||Accounts().Single(x=>x.Id==cash).Kind!="Cash")) throw new InvalidOperationException("اختر الصندوق أو البنك / Choose cash or bank");
        using var tx=store.BeginTransaction(); long id=NewDoc(sale?"SI":"PI",date,party,cash,note,t,p,vat,inclusive);
        long goods=0,services=0,cost=0;
        foreach(var l in lines) {
            var item=Items().Single(x=>x.Id==l.Item); long q=M(l.Qty); var lt=Calculate([l],vat,inclusive); long lineCost=0;
            if(item.Stock) {
                lineCost=sale?0:lt.Net;
                Exec("UPDATE items SET qty=qty+@p0,value=value+@p1 WHERE id=@p2",sale?-q:q,sale?-lineCost:lineCost,item.Id);
                Exec("INSERT INTO stock_moves(doc,item,qty,value) VALUES(@p0,@p1,@p2,@p3)",id,item.Id,sale?-q:q,sale?-lineCost:lineCost);
                goods+=lt.Net; if(sale) cost+=lineCost;
            } else services+=lt.Net;
            Exec("INSERT INTO invoice_lines(doc,item,description,qty,price,discount,net,tax,cost) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)",id,item.Id,l.Description,q,M(l.Price),M(l.Discount),lt.Net,lt.Tax,lineCost);
        }
        List<EntryLine> jl=[];
        if(sale) {
            jl.Add(new(party,t.Total-p,0)); if(p>0) jl.Add(new(cash!.Value,p,0)); jl.Add(new(AccountId("4101"),0,t.Net)); jl.Add(new(AccountId("2102"),0,t.Tax));
            jl.Add(new(AccountId("5104"),cost,0)); jl.Add(new(AccountId("1301"),0,cost));
        } else {
            jl.Add(new(AccountId("1301"),goods,0)); jl.Add(new(AccountId("5101"),services,0)); jl.Add(new(AccountId("1401"),t.Tax,0)); jl.Add(new(party,0,t.Total-p)); if(p>0) jl.Add(new(cash!.Value,0,p));
        }
        Journal(id,jl);SnapshotBranding(id);RebuildInventory("invoice "+id);tx.Commit(); return id;
    }
    public long OpeningStock(long itemId,DateTime date,decimal quantity,decimal unitCost)
    {
        long q=M(quantity),v=M(quantity*unitCost); var item=Items().Single(x=>x.Id==itemId);
        if(!item.Stock||q<=0||v<=0||D(q)!=quantity||D(M(unitCost))!=unitCost||Convert.ToInt64(Scalar("SELECT count(*) FROM stock_moves WHERE item=@p0",itemId))>0) throw new InvalidOperationException("رصيد افتتاحي لصنف مخزني بدون حركات سابقة، بكمية وتكلفة موجبتين / Opening stock requires unused stock item and positive quantity/cost");
        using var tx=store.BeginTransaction(); long id=NewDoc("OB",date,null,null,item.Name,new(v,0,v));
        Exec("UPDATE items SET qty=@p0,value=@p1 WHERE id=@p2",q,v,itemId); Exec("INSERT INTO stock_moves(doc,item,qty,value) VALUES(@p0,@p1,@p2,@p3)",id,itemId,q,v);
        Journal(id,[new(AccountId("1301"),v,0),new(AccountId("3201"),0,v)]);RebuildInventory("opening stock "+id);tx.Commit();return id;
    }
    public long Reverse(long id,string reason,DateTime? reversalDate=null,bool managed=false)
    {
        if(string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("اكتب سبب الإلغاء / Enter a cancellation reason");
        using var tx=store.BeginTransaction();var effectiveDate=reversalDate??DateTime.Today; var doc=Table("SELECT * FROM documents WHERE id=@p0",id).Rows[0];
        if((long)doc["reversed"]==1||doc["kind"].ToString()=="RV") throw new InvalidOperationException("المستند ملغى بالفعل / Document already cancelled");
        if(effectiveDate.Date<DateTime.Parse((string)doc["date"]))throw new InvalidOperationException("Cancellation date precedes document date");
        if(!managed&&(doc["kind"].ToString() is "CR" or "CP" or "CC" or "CL"))throw new InvalidOperationException("استخدم شاشة الشيكات أو الفترات المالية / Use the cheque register or financial periods screen");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM documents WHERE original=@p0 AND kind IN ('SR','PR') AND reversed=0",id))>0)throw new InvalidOperationException("ألغِ المرتجعات المرتبطة أولًا / Cancel linked returns first");
        var moves=Table("SELECT * FROM stock_moves WHERE doc=@p0",id);
        EnsureOpen(DateTime.Parse((string)doc["date"]));
        long reversal=NewDoc("RV",effectiveDate,null,null,reason,new((long)doc["total"],0,(long)doc["total"]));
        Exec("UPDATE documents SET original=@p0 WHERE id=@p1",id,reversal);
        var original=Table("SELECT * FROM entries WHERE doc=@p0",id).Rows.Cast<DataRow>().Select(r=>new EntryLine((long)r["account"],(long)r["credit"],(long)r["debit"])); Journal(reversal,original);
        Exec("UPDATE documents SET reversed=1,reversal_date=@p0 WHERE id=@p1",effectiveDate.ToString("yyyy-MM-dd"),id);Exec("UPDATE allocations SET revoked=@p0 WHERE (invoice=@p1 OR voucher=@p1) AND revoked IS NULL",effectiveDate.ToString("yyyy-MM-dd"),id);foreach(DataRow m in moves.Rows)Exec("INSERT INTO stock_moves(doc,item,qty,value) VALUES(@p0,@p1,@p2,@p3)",reversal,m["item"],-(long)m["qty"],-(long)m["value"]);RebuildInventory("reverse "+id);Audit("reverse",$"{doc["number"]}: {reason}");tx.Commit();return reversal;
    }
}


