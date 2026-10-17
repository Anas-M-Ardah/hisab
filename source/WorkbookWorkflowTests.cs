using System.IO;
namespace Hisab;
public static class WorkbookWorkflowTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(InvalidOperationException){results.Add("PASS "+name);return;}throw new Exception("Did not reject: "+name);}
        string path=Path.Combine(directory,"workflows-"+Guid.NewGuid()+".db");using var s=new Store(path);var a=new AccountingService(s);var day=DateTime.Today;long cash=s.AccountId("1101");
        s.SaveAccount(null,"WX","Expenses","Expense");long expenses=s.AccountId("WX");s.SaveAccount(null,"WR","Rent","Expense",expenses);long rent=s.AccountId("WR");s.SaveAccount(null,"WO","Orders","Asset");long orders=s.AccountId("WO");s.SaveAccount(null,"WO1","Order 1","Asset",orders);long order=s.AccountId("WO1");
        long payment=a.SplitVoucher(false,day,cash,"Workbook payment",[new(order,55.6m,"Customs clearance"),new(rent,2500,"Rent for 2026")]);
        Check(s.Balance(order)==55600&&s.Balance(rent)==2500000&&s.Balance(cash)==-2555600,"Workbook split payment posts each allocation and cash once");
        Check(s.Table("SELECT * FROM entries WHERE doc=@p0 AND description='Customs clearance'",payment).Rows.Count==1,"Split payment preserves line descriptions");
        var roots=s.HierarchyTrial(day,day,null,false);Check(roots.Single(r=>r.Account==orders).Debit==55600&&roots.Sum(r=>r.Balance)==0,"Main trial rolls up families without double counting");
        a.Voucher(false,day.AddDays(-1),expenses,cash,10,"Legacy parent posting");
        var children=s.HierarchyTrial(day,day,expenses,false);Check(children.Single(r=>r.Direct).Balance==10000&&children.Single(r=>r.Direct).Debit==0&&children.Single(r=>r.Account==rent).Debit==2500000,"Child trial retains direct parent opening balances outside period movements");
        long count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
        Reject(()=>a.SplitVoucher(false,day,cash,"Bad",[new(rent,1.0001m,"Precision")]),"Split voucher rejects excess precision");
        Reject(()=>a.SplitVoucher(false,day,cash,"Bad",[new(cash,10,"Same cash")]),"Split voucher rejects cash as its own counterpart");
        Reject(()=>a.SplitVoucher(false,day,cash,"Bad",[new(rent,10,"")]),"Split voucher requires each line description");
        Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count,"Invalid split vouchers leave no partial documents");
        long replacement=a.AmendSplitVoucher(payment,day,cash,"Corrected",[new(order,100,"Order cost"),new(rent,2400,"Rent")],"Correct amount");
        Check(s.Balance(order)==100000&&s.Balance(rent)==2400000&&(long)s.Scalar("SELECT replaced_by FROM documents WHERE id=@p0",payment)! ==replacement,"Split amendment reverses original and retains replacement link");
        Reject(()=>a.AmendSimple(replacement,day,rent,cash,5,"Wrong editor",null,"reason"),"Single account editor cannot flatten a split voucher");
        var item=s.Items().First(i=>i.Stock);long transfer=a.TransferOrderToStock(day,order,"Receive order 1",[new(item.Id,2,100)]);
        Check(s.Balance(order)==0&&s.Items().Single(i=>i.Id==item.Id).Qty==2000&&s.Items().Single(i=>i.Id==item.Id).Value==100000,"Order transfer moves accumulated cost into received stock");
        Check(s.Balance(s.AccountId("1301"))==s.Items().Sum(i=>i.Value),"Order transfer reconciles inventory ledger and valuation");
        Reject(()=>a.TransferOrderToStock(day,order,"Too much",[new(item.Id,1,1)]),"Order transfer cannot exceed available order cost");
        Reject(()=>a.TransferOrderToStock(day,expenses,"Wrong kind",[new(item.Id,1,1)]),"Order transfer requires an asset account");
        s.SaveAccount(null,"WC","Customer","Customer");long customer=s.AccountId("WC");long sale=a.Invoice(true,day,customer,null,0,0,false,"Sale after transfer",[new(item.Id,"Transferred item",1,80,0)]);
        Check(s.Items().Single(i=>i.Id==item.Id).Value==50000,"Sale after order transfer uses transferred unit cost");
        Reject(()=>a.Reverse(transfer,"Cannot remove consumed stock"),"Consumed order transfer cannot reverse into negative stock");
        Check((long)s.Scalar("SELECT reversed FROM documents WHERE id=@p0",transfer)! ==0,"Failed transfer reversal rolls back atomically");
        a.Reverse(sale,"Undo sale");a.Reverse(transfer,"Undo transfer");Check(s.Balance(order)==100000&&s.Items().Single(i=>i.Id==item.Id).Qty==0&&s.Balance(s.AccountId("1301"))==0,"Order transfer reversal restores order cost and stock");
        var ids=a.ReceiveCheques(day,customer,3294.41m,"Six cheques",new[]{500m,500m,1294.41m,500m,250m,250m}.Select((v,i)=>new ReceiptCheque("WB"+i,"Bank",v,day.AddMonths(i))).ToList());
        Check(ids.Count==6&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents WHERE receipt_group=@p0",ids[0]))==6&&Convert.ToInt64(s.Scalar("SELECT sum(total) FROM documents WHERE receipt_group=@p0",ids[0]))==3294410,"Workbook six-cheque receipt shares one reference and exact total");
        Check(s.Table("SELECT doc FROM entries GROUP BY doc HAVING sum(debit)!=sum(credit)").Rows.Count==0,"All workbook workflow documents remain balanced");
        Check(s.Table("PRAGMA foreign_key_check").Rows.Count==0,"Workbook workflow relationships preserve database integrity");
        using(var reopened=new Store(path))Check(reopened.Scalar("SELECT description FROM entries WHERE doc=@p0 AND account=@p1",replacement,order)?.ToString()=="Order cost"&&Convert.ToInt64(reopened.Scalar("PRAGMA user_version"))==3,"Workflow data survives reopening with schema downgrade protection");
        s.SaveUser("workbook-viewer","TestPassword123","Viewer");s.Login("workbook-viewer","TestPassword123");Reject(()=>a.SplitVoucher(false,day,cash,"No permission",[new(rent,1,"Rent")]),"Viewer cannot post split vouchers");
    }
}
