using System.Data;

namespace Hisab;
public record PartyBalance(long Account,string Kind,long Ledger,long InvoiceOutstanding,long OutsideAging);
public sealed partial class AccountingService
{
    // Positive means receivable for customers, payable for suppliers. The
    // residual is disclosed for review, never assigned a fictional due date.
    public List<PartyBalance> PartyReconciliation(DateTime asOf)
    {
        var result=new List<PartyBalance>();
        foreach(DataRow row in Table("SELECT a.id,a.kind,coalesce(sum(e.debit-e.credit),0) balance FROM accounts a LEFT JOIN entries e ON e.account=a.id AND e.doc IN (SELECT id FROM documents WHERE date<=@p0) WHERE a.kind IN ('Customer','Supplier') GROUP BY a.id,a.kind ORDER BY a.code",asOf.ToString("yyyy-MM-dd")).Rows){
            long id=(long)row["id"];string kind=(string)row["kind"];
            long ledger=(long)row["balance"]*(kind=="Supplier"?-1:1);
            long invoices=Positions(asOf,id,kind=="Customer"?"SI":"PI").Sum(p=>p.Outstanding);
            if(ledger!=0||invoices!=0)result.Add(new(id,kind,ledger,invoices,ledger-invoices));
        }
        return result;
    }
}
