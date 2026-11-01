using System.Data;
using static Hisab.Store;
namespace Hisab;
public record PurchaseOrderLine(long Item,decimal Quantity,decimal UnitCost);
public record ReceiptInput(long OrderLine,decimal Quantity,string Serials);
public record BillInput(long ReceiptLine,decimal Quantity,decimal UnitCost);
public sealed partial class AccountingService
{
    public long CreatePurchaseOrder(long supplier,long location,DateTime expected,IReadOnlyList<PurchaseOrderLine> lines)
    {
        store.Require("post");RequireLocation(location);
        if(!Accounts().Any(a=>a.Id==supplier&&a.Kind=="Supplier")||lines.Count==0||lines.Any(l=>l.Quantity<=0||l.UnitCost<=0||D(M(l.Quantity))!=l.Quantity||D(M(l.UnitCost))!=l.UnitCost||!Items().Any(i=>i.Id==l.Item&&i.Stock)||(store.IsSerialized(l.Item)&&l.Quantity!=decimal.Truncate(l.Quantity))))InventoryError("Choose a supplier and stock lines with positive quantities/costs");
        using var tx=store.BeginTransaction();Exec("INSERT INTO purchase_orders(supplier,location,expected) VALUES(@p0,@p1,@p2)",supplier,location,expected.ToString("yyyy-MM-dd"));long id=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));
        foreach(var l in lines)Exec("INSERT INTO purchase_order_lines(order_id,item,qty,price) VALUES(@p0,@p1,@p2,@p3)",id,l.Item,M(l.Quantity),M(l.UnitCost));Audit("purchase-order",id.ToString());tx.Commit();return id;
    }
    public void IssuePurchaseOrder(long id){store.Require("post");if(Scalar("SELECT state FROM purchase_orders WHERE id=@p0",id)?.ToString()!="Draft")InventoryError("Choose a draft order");using var tx=store.BeginTransaction();Exec("UPDATE purchase_orders SET state='Issued' WHERE id=@p0",id);Audit("order-issued",id.ToString());tx.Commit();}
    public void CancelPurchaseOrder(long id){store.Require("post");if(Scalar("SELECT state FROM purchase_orders WHERE id=@p0",id)==null)InventoryError("Unknown order");using var tx=store.BeginTransaction();Exec("UPDATE purchase_orders SET state='Cancelled' WHERE id=@p0",id);Audit("order-cancelled",id.ToString());tx.Commit();}
    public void AmendPurchaseOrderLine(long line,decimal quantity,decimal unitCost,DateTime expected,string reason)
    {
        store.Require("post");var l=Table("SELECT l.*,o.state FROM purchase_order_lines l JOIN purchase_orders o ON o.id=l.order_id WHERE l.id=@p0",line).Rows[0];
        if(string.IsNullOrWhiteSpace(reason)||l["state"].ToString()=="Cancelled"||quantity<=0||unitCost<=0||D(M(quantity))!=quantity||D(M(unitCost))!=unitCost||M(quantity)<ReceivedQuantity(line)||(store.IsSerialized((long)l["item"])&&quantity!=decimal.Truncate(quantity)))InventoryError("Order amendment cannot reduce received quantities; enter valid cost and reason");
        using var tx=store.BeginTransaction();Exec("UPDATE purchase_order_lines SET qty=@p0,price=@p1 WHERE id=@p2",M(quantity),M(unitCost),line);Exec("UPDATE purchase_orders SET expected=@p0 WHERE id=@p1",expected.ToString("yyyy-MM-dd"),l["order_id"]);RefreshPurchaseOrders();Audit("order-amend",line+": "+reason);tx.Commit();
    }
    public long ReceivedQuantity(long orderLine)=>Convert.ToInt64(Scalar("SELECT coalesce(sum(l.qty),0) FROM receipt_links r JOIN invoice_lines l ON l.id=r.line JOIN documents d ON d.id=l.doc WHERE r.order_line=@p0 AND d.reversed=0",orderLine))-Convert.ToInt64(Scalar("SELECT coalesce(sum(ret.qty),0) FROM receipt_links r JOIN invoice_lines ret ON ret.source_line=r.line JOIN documents d ON d.id=ret.doc WHERE r.order_line=@p0 AND d.kind='GU' AND d.reversed=0",orderLine));
    public long BilledQuantity(long receiptLine)=>Convert.ToInt64(Scalar("SELECT coalesce(sum(l.qty),0) FROM receipt_bills b JOIN invoice_lines l ON l.id=b.line JOIN documents d ON d.id=l.doc WHERE b.receipt_line=@p0 AND d.reversed=0",receiptLine));
    public long UnbilledQuantity(long receiptLine){var l=Table("SELECT qty FROM invoice_lines WHERE id=@p0",receiptLine).Rows[0];return (long)l["qty"]-BilledQuantity(receiptLine)-Convert.ToInt64(Scalar("SELECT coalesce(sum(l.qty),0) FROM invoice_lines l JOIN documents d ON d.id=l.doc WHERE l.source_line=@p0 AND d.kind='GU' AND d.reversed=0",receiptLine));}
    void RefreshPurchaseOrders()
    {
        foreach(DataRow o in Table("SELECT id FROM purchase_orders WHERE state NOT IN ('Draft','Cancelled')").Rows){var lines=Table("SELECT id,qty FROM purchase_order_lines WHERE order_id=@p0",o["id"]).Rows.Cast<DataRow>().ToList();bool all=lines.All(l=>ReceivedQuantity((long)l["id"])>=(long)l["qty"]),any=lines.Any(l=>ReceivedQuantity((long)l["id"])>0);Exec("UPDATE purchase_orders SET state=@p0 WHERE id=@p1",all?"Received":any?"Partial":"Issued",o["id"]);}
    }
    public long ReceivePurchaseOrder(long order,DateTime date,IReadOnlyList<ReceiptInput> inputs)
    {
        var t=Table("SELECT * FROM purchase_orders WHERE id=@p0",order);if(t.Rows.Count!=1||t.Rows[0]["state"].ToString() is "Draft" or "Cancelled")InventoryError("Issue the purchase order before receiving");var o=t.Rows[0];RequireLocation((long)o["location"]);
        if(inputs.Count==0||inputs.Select(i=>i.OrderLine).Distinct().Count()!=inputs.Count)InventoryError("Choose distinct order lines");
        using var tx=store.BeginTransaction();long id=NewDoc("GR",date,(long)o["supplier"],null,"PO-"+order,new(0,0,0));long total=0;
        foreach(var input in inputs){var rows=Table("SELECT * FROM purchase_order_lines WHERE id=@p0 AND order_id=@p1",input.OrderLine,order);if(rows.Rows.Count!=1)InventoryError("Line is not part of this order");var l=rows.Rows[0];long q=M(input.Quantity);if(q<=0||D(q)!=input.Quantity||q>(long)l["qty"]-ReceivedQuantity(input.OrderLine))InventoryError("Receipt exceeds outstanding order quantity; amend the order first");long net=M(input.Quantity*D((long)l["price"]));total+=net;
            Exec("INSERT INTO invoice_lines(doc,item,description,qty,price,discount,net,tax,cost,location) VALUES(@p0,@p1,@p2,@p3,@p4,0,@p5,0,0,@p6)",id,l["item"],Items().Single(i=>i.Id==(long)l["item"]).Name,q,l["price"],net,o["location"]);long line=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));Exec("INSERT INTO receipt_links(line,order_line) VALUES(@p0,@p1)",line,input.OrderLine);RecordSerials(id,line,(long)l["item"],(long)o["location"],q,input.Serials,1,true);
        }
        Exec("UPDATE documents SET net=@p0,total=@p0 WHERE id=@p1",total,id);SnapshotBranding(id);RebuildInventory("receipt "+id);tx.Commit();return id;
    }
    public long BillReceipts(long supplier,DateTime date,IReadOnlyList<BillInput> inputs,decimal vat)
    {
        if(inputs.Count==0||inputs.Select(i=>i.ReceiptLine).Distinct().Count()!=inputs.Count||vat<0||vat>100||!Accounts().Any(a=>a.Id==supplier&&a.Kind=="Supplier"))InventoryError("Choose supplier and distinct receipt lines with a valid tax rate");
        using var tx=store.BeginTransaction();long id=NewDoc("PI",date,supplier,null,"Receipt billing",new(0,0,0),0,vat);long net=0,tax=0;
        foreach(var input in inputs){var rows=Table("SELECT l.*,d.date receipt_date FROM invoice_lines l JOIN documents d ON d.id=l.doc WHERE l.id=@p0 AND d.kind='GR' AND d.party=@p1 AND d.reversed=0",input.ReceiptLine,supplier);if(rows.Rows.Count!=1)InventoryError("Choose an active goods receipt for this supplier");var r=rows.Rows[0];EnsureOpen(DateTime.Parse((string)r["receipt_date"]));if(date.Date<DateTime.Parse((string)r["receipt_date"]))InventoryError("Invoice cannot precede receipt");long q=M(input.Quantity),remaining=UnbilledQuantity(input.ReceiptLine);if(q<=0||D(q)!=input.Quantity||q>remaining||input.UnitCost<=0||D(M(input.UnitCost))!=input.UnitCost||(store.IsSerialized((long)r["item"])&&q%1000!=0))InventoryError("Bill only remaining received quantities at a positive cost");
            long n=M(input.Quantity*input.UnitCost),v=M(D(n)*vat/100),basis=M(D((long)r["net"])*q/(long)r["qty"]);net+=n;tax+=v;
            Exec("INSERT INTO invoice_lines(doc,item,description,qty,price,discount,net,tax,cost,location) VALUES(@p0,@p1,@p2,@p3,@p4,0,@p5,@p6,0,@p7)",id,r["item"],r["description"],q,M(input.UnitCost),n,v,r["location"]);long line=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));Exec("INSERT INTO receipt_bills(line,receipt_line,base_value) VALUES(@p0,@p1,@p2)",line,input.ReceiptLine,basis);
        }
        Exec("UPDATE documents SET net=@p0,tax=@p1,total=@p2 WHERE id=@p3",net,tax,net+tax,id);SnapshotBranding(id);RebuildInventory("receipt invoice "+id);tx.Commit();return id;
    }
    long ReceiptExtra(long line)=>Convert.ToInt64(Scalar("SELECT coalesce(sum(l.net-b.base_value),0) FROM receipt_bills b JOIN invoice_lines l ON l.id=b.line JOIN documents d ON d.id=l.doc WHERE b.receipt_line=@p0 AND d.reversed=0",line))+Convert.ToInt64(Scalar("SELECT coalesce(sum(a.amount),0) FROM landed_allocations a JOIN documents d ON d.id=a.doc WHERE a.receipt_line=@p0 AND d.reversed=0",line));
    bool IsReceiptBill(long doc)=>Convert.ToInt64(Scalar("SELECT count(*) FROM receipt_bills b JOIN invoice_lines l ON l.id=b.line WHERE l.doc=@p0",doc))>0;
    void ReplayReceiptBill(DataRow doc)
    {
        long id=(long)doc["id"],basis=Convert.ToInt64(Scalar("SELECT coalesce(sum(b.base_value),0) FROM receipt_bills b JOIN invoice_lines l ON l.id=b.line WHERE l.doc=@p0",id)),net=(long)doc["net"],tax=(long)doc["tax"];var lines=new List<EntryLine>{new(AccountId("2195"),basis,0),new((long)doc["party"],0,net+tax)};long delta=net-basis;if(delta>0)lines.Add(new(AccountId("2196"),delta,0));if(delta<0)lines.Add(new(AccountId("2196"),0,-delta));if(tax>0)lines.Add(new(AccountId("1401"),tax,0));Exec("DELETE FROM entries WHERE doc=@p0",id);Exec("DELETE FROM stock_moves WHERE doc=@p0",id);Journal(id,lines);
    }
    void CheckPurchasingReversal(long doc)
    {
        foreach(DataRow r in Table("SELECT DISTINCT d.date FROM receipt_bills b JOIN invoice_lines billed ON billed.id=b.line JOIN invoice_lines receipt ON receipt.id=b.receipt_line JOIN documents d ON d.id=receipt.doc WHERE billed.doc=@p0 UNION SELECT d.date FROM landed_allocations a JOIN invoice_lines l ON l.id=a.receipt_line JOIN documents d ON d.id=l.doc WHERE a.doc=@p0",doc).Rows)EnsureOpen(DateTime.Parse((string)r[0]));
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM receipt_bills b JOIN invoice_lines r ON r.id=b.receipt_line JOIN invoice_lines l ON l.id=b.line JOIN documents d ON d.id=l.doc WHERE r.doc=@p0 AND d.reversed=0",doc))>0||Convert.ToInt64(Scalar("SELECT count(*) FROM landed_allocations a JOIN invoice_lines l ON l.id=a.receipt_line JOIN documents d ON d.id=a.doc WHERE l.doc=@p0 AND d.reversed=0",doc))>0)InventoryError("Reverse linked supplier invoices and landed costs first");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM documents WHERE original=@p0 AND kind='GU' AND reversed=0",doc))>0)InventoryError("Reverse linked goods returns first");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM landed_sources s JOIN documents d ON d.id=s.doc WHERE s.source=@p0 AND d.reversed=0",doc))>0)InventoryError("Reverse the linked landed-cost allocation first");
    }
}
