using System.IO;
namespace Hisab;
public static class ClientFeedbackTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(InvalidOperationException){results.Add("PASS "+name);return;}throw new Exception("Did not reject: "+name);}
        string path=Path.Combine(directory,"feedback.db");
        using(var s=new Store(path))
        {
            var a=new AccountingService(s);var day=DateTime.Today;long cash=s.AccountId("1101");
            s.SaveAccount(null,"EXP","Expenses","Expense");long root=s.AccountId("EXP");
            s.SaveAccount(null,"WATER","Water","Expense",root);long water=s.AccountId("WATER");
            s.SaveAccount(null,"METER","Meter","Expense",water);long meter=s.AccountId("METER");
            a.Voucher(false,day,water,cash,10,"Water");a.Voucher(false,day,meter,cash,5,"Meter");
            Check(s.Balance(root)==0&&s.GroupBalance(root)==15000&&s.GroupBalance(water)==15000,"Hierarchy rolls up all descendants without posting to parent");
            Reject(()=>s.SaveAccount(root,"EXP","Expenses","Expense",meter),"Descendant cannot become parent");
            Reject(()=>s.SaveAccount(water,"WATER","Water","Expense",water),"Self parent rejected");
            Reject(()=>s.SaveAccount(null,"BAD","Wrong type","Customer",root),"Cross-type account parent rejected");
            Reject(()=>s.SaveAccount(root,"EXP","Expenses","Income"),"Parent type cannot conflict with children");
            s.SaveAccount(meter,"METER","Meter","Expense",root);Check(s.Accounts().Single(x=>x.Id==meter).ParentId==root&&s.GroupBalance(water)==10000,"Reparenting moves rollup without changing journals");
            s.SaveAccount(null,"CUST","Customer","Customer");s.SaveAccount(null,"SUP","Supplier","Supplier");long customer=s.AccountId("CUST"),supplier=s.AccountId("SUP");
            var item=s.Items()[0];long purchase=a.Invoice(false,day,supplier,null,0,0,false,"Purchase",[new(item.Id,"Goods",10,1,0)]);
            long sale=a.Invoice(true,day,customer,null,0,0,false,"Sale",[new(item.Id,"Goods",3,2,0)]);
            var flow=s.StockFlows()[item.Id];Check(flow.In==10000&&flow.Out==3000&&flow.In-flow.Out==s.Items()[0].Qty,"Stock In and Out reconcile with available quantity");
            a.Reverse(sale,"Undo sale");flow=s.StockFlows()[item.Id];Check(flow.In==13000&&flow.Out==3000&&flow.In-flow.Out==s.Items()[0].Qty,"Stock reversal appears in opposite movement column");
            long invoice=a.Invoice(true,day,customer,null,0,0,false,"Sale for cheques",[new(item.Id,"Goods",2,50,0)]);
            var ids=a.ReceiveCheques(day,customer,100,"Two cheques",[new("A1","Bank",40,day.AddDays(10)),new("A2","Bank",60,day.AddDays(20))],invoice);
            Check(ids.Count==2&&ids.Distinct().Count()==2&&a.Positions(day).Single(p=>p.Invoice==invoice).Outstanding==0,"Multiple cheques post separately and allocate receipt total");
            Check(s.Scalar("SELECT due_date FROM documents WHERE id=@p0",ids[1])?.ToString()==day.AddDays(20).ToString("yyyy-MM-dd"),"Cheque due date retained on document");
            Check(s.Scalar("SELECT note FROM documents WHERE id=@p0",ids[0])?.ToString()?.Contains("Two cheques")==true,"Cheque receipt description retained");
            long first=Convert.ToInt64(s.Scalar("SELECT id FROM cheques WHERE issue_doc=@p0",ids[0]));long second=Convert.ToInt64(s.Scalar("SELECT id FROM cheques WHERE issue_doc=@p0",ids[1]));
            a.SettleCheque(first,"Cleared",cash,day,"Clear first");a.SettleCheque(second,"Bounced",cash,day,"Bounce second");
            Check(a.Positions(day).Single(p=>p.Invoice==invoice).Outstanding==60000&&s.Scalar("SELECT state FROM cheques WHERE id=@p0",first)?.ToString()=="Cleared","Bouncing one cheque leaves sibling cleared and restores only its allocation");
            long count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
            Reject(()=>a.ReceiveCheques(day,customer,10,"Mismatch",[new("M1","Bank",9,day)]),"Mismatched receipt and cheque totals rejected");
            Reject(()=>a.ReceiveCheques(day,customer,10,"Duplicate",[new("A1","Bank",10,day)]),"Previously recorded cheque rejected");
            Reject(()=>a.ReceiveCheques(day,customer,10,"Duplicate batch",[new("D1","Bank",5,day),new("D1","Bank",5,day)]),"Duplicate cheque within batch rejected");
            Reject(()=>a.ReceiveCheques(day,customer,10,"Bad due",[new("B1","Bank",10,day.AddDays(-1))]),"Cheque due date before receipt rejected");
            Reject(()=>a.ReceiveCheques(day,customer,10,"Precision",[new("P1","Bank",10.0001m,day)]),"Cheque precision beyond three decimals rejected");
            Reject(()=>a.ReceiveCheques(day,customer,10,"Rollback",[new("R1","Bank",5,day),new("R2","Bank",5,day)],purchase),"Invalid invoice allocation rolls back entire batch");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM cheques WHERE number IN ('R1','R2')"))==0,"Rejected batch leaves no cheques or documents");
            s.SaveAccount(meter,"METER","Meter","Expense");Check(s.Accounts().Single(x=>x.Id==meter).ParentId==null,"Child can return to top level");
            s.Set("font","14");Check(s.Setting("font")=="14","Small font preference persisted");
            Check(s.Table("PRAGMA foreign_key_check").Rows.Count==0,"Feedback changes preserve foreign keys");
        }
        using(var reopened=new Store(path)){Check(reopened.Accounts().Single(x=>x.Code=="WATER").ParentId==reopened.AccountId("EXP"),"Hierarchy persists after reopening");Check(reopened.Setting("font")=="14","Font survives reopening");}
        string legacy=Path.Combine(directory,"feedback-legacy.db");using(var old=new Store(legacy)){old.Exec("DROP INDEX ix_accounts_parent");old.Exec("ALTER TABLE accounts DROP COLUMN parent_id");}
        using(var upgraded=new Store(legacy)){Check(upgraded.Accounts().All(x=>x.ParentId==null),"Existing accounts migrate as top-level accounts");}
    }
}
