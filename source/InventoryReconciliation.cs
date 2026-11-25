using System.Data;
using static Hisab.Store;
namespace Hisab;
public record HistoricalStockRow(long Item,long Location,long Quantity,long Value);
public record HistoricalStock(List<HistoricalStockRow> Rows,long StockValue,long LedgerValue,long Difference);
public record ReceiptAccrualRow(long Receipt,long Line,long Supplier,long Item,string Number,string Date,long Quantity,long Value);
public record ReceiptAccrualReport(List<ReceiptAccrualRow> Rows,long PendingValue,long LedgerValue,long Difference);
public sealed partial class AccountingService
{
    public HistoricalStock InventoryAsOf(DateTime cutoff)
    {
        string date=cutoff.ToString("yyyy-MM-dd");var rows=new List<HistoricalStockRow>();
        var totals=Table("SELECT m.item,sum(m.qty) qty,sum(m.value) value FROM stock_moves m JOIN documents d ON d.id=m.doc WHERE d.date<=@p0 GROUP BY m.item",date);
        // Legacy reversal moves omit the original location. Use each original
        // movement's effective interval for location quantities, not the RV location.
        var locations=Table("SELECT m.item,m.location,sum(m.qty) qty FROM stock_moves m JOIN documents d ON d.id=m.doc WHERE d.kind!='RV' AND d.date<=@p0 AND (d.reversal_date IS NULL OR d.reversal_date>@p0) GROUP BY m.item,m.location HAVING sum(m.qty)!=0 ORDER BY m.item,m.location",date).Rows.Cast<DataRow>().ToList();
        foreach(DataRow total in totals.Rows){long item=(long)total["item"],qty=(long)total["qty"],value=(long)total["value"];var itemLocations=locations.Where(r=>(long)r["item"]==item).ToList();
            if(itemLocations.Sum(r=>(long)r["qty"])!=qty)throw new InvalidOperationException("Historical location quantities do not reconcile / كميات المواقع التاريخية غير متطابقة");
            // A zero company quantity can still contain opposite warehouse balances.
            // No company average exists here; retain quantities and disclose residual value separately.
            if(qty==0){foreach(var location in itemLocations)rows.Add(new(item,(long)location["location"],(long)location["qty"],0));if(value!=0)rows.Add(new(item,0,0,value));continue;}
            long assigned=0;for(int i=0;i<itemLocations.Count;i++){var location=itemLocations[i];long allocated=i==itemLocations.Count-1?value-assigned:M(D(value)*(long)location["qty"]/qty);assigned+=allocated;rows.Add(new(item,(long)location["location"],(long)location["qty"],allocated));}
        }
        long stock=totals.Rows.Cast<DataRow>().Sum(r=>(long)r["value"]),ledger=LedgerBalances(cutoff).GetValueOrDefault(AccountId("1301"));return new(rows,stock,ledger,stock-ledger);
    }
    public ReceiptAccrualReport ReceiptAccruals(DateTime cutoff)
    {
        string date=cutoff.ToString("yyyy-MM-dd");var rows=new List<ReceiptAccrualRow>();
        foreach(DataRow r in Table("SELECT d.id receipt,d.number,d.date,d.party,l.id line,l.item,l.qty,l.net FROM documents d JOIN invoice_lines l ON l.doc=d.id WHERE d.kind='GR' AND d.date<=@p0 AND (d.reversal_date IS NULL OR d.reversal_date>@p0) ORDER BY d.date,d.id,l.id",date).Rows){
            long line=(long)r["line"];
            var billed=Table("SELECT coalesce(sum(l.qty),0) qty,coalesce(sum(b.base_value),0) value FROM receipt_bills b JOIN invoice_lines l ON l.id=b.line JOIN documents d ON d.id=l.doc WHERE b.receipt_line=@p0 AND d.date<=@p1 AND (d.reversal_date IS NULL OR d.reversal_date>@p1)",line,date).Rows[0];
            var returned=Table("SELECT coalesce(sum(l.qty),0) qty,coalesce(sum(l.net),0) value FROM invoice_lines l JOIN documents d ON d.id=l.doc WHERE l.source_line=@p0 AND d.kind='GU' AND d.date<=@p1 AND (d.reversal_date IS NULL OR d.reversal_date>@p1)",line,date).Rows[0];
            long qty=(long)r["qty"]-(long)billed["qty"]-(long)returned["qty"],value=(long)r["net"]-(long)billed["value"]-(long)returned["value"];
            if(qty!=0||value!=0)rows.Add(new((long)r["receipt"],line,(long)r["party"],(long)r["item"],(string)r["number"],(string)r["date"],qty,value));
        }
        long pending=rows.Sum(r=>r.Value),ledger=-LedgerBalances(cutoff).GetValueOrDefault(AccountId("2195"));return new(rows,pending,ledger,pending-ledger);
    }
}
