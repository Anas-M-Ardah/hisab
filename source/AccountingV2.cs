using System.Data;
using System.Globalization;
using System.Text.Json;
using static Hisab.Store;

namespace Hisab;
public record ReturnLine(long SourceLine,decimal Quantity);
public record InvoicePosition(long Invoice,string Number,string Party,string Kind,string Date,string Due,long Total,long Paid,long Returned,long Outstanding);
public sealed partial class AccountingService
{
    int deferredRebuild;
    void EnsureOpen(DateTime date)
    {
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM periods WHERE @p0<=end",date.ToString("yyyy-MM-dd")))>0)throw new InvalidOperationException("الفترة المالية مقفلة أو التاريخ يسبق آخر إقفال / Date is in or before the last closed period");
    }
    void SnapshotBranding(long id)
    {
        var row=Table("SELECT party,date FROM documents WHERE id=@p0",id).Rows[0];var names=Table("SELECT name,name_ar,name_en,address_ar,address_en FROM accounts WHERE id=@p0",row["party"]).Rows[0];
        var branding=new Dictionary<string,string>();foreach(var key in new[]{"company_en","terms_en","warranty_en","shipping_en","bank_en"})branding[key]=store.Setting(key);
        foreach(var key in new[]{"name","name_ar","name_en","address_ar","address_en"})branding["party_"+key]=names[key].ToString()??"";
        byte[]? Image(string setting){var text=store.Setting(setting);return string.IsNullOrEmpty(text)?null:Convert.FromBase64String(text);}
        var due=DateTime.Parse((string)row["date"]).AddDays(int.Parse(store.Setting("due_days","30"),CultureInfo.InvariantCulture));
        Exec("UPDATE documents SET branding=@p0,logo=@p1,seal=@p2,due_date=@p3 WHERE id=@p4",JsonSerializer.Serialize(branding),Image("logo"),Image("seal"),due.ToString("yyyy-MM-dd"),id);
    }
    public List<InvoicePosition> Positions(DateTime asOf,long? party=null,string? kind=null)
    {
        string date=asOf.ToString("yyyy-MM-dd");var result=new List<InvoicePosition>();
        foreach(DataRow d in Table("SELECT d.*,a.name party_name FROM documents d JOIN accounts a ON a.id=d.party WHERE d.kind IN ('SI','PI') AND d.date<=@p0 AND (d.reversal_date IS NULL OR d.reversal_date>@p0) AND (@p1 IS NULL OR d.party=@p1) AND (@p2 IS NULL OR d.kind=@p2) ORDER BY d.due_date,d.id",date,party,kind).Rows){
            long id=(long)d["id"],returned=Convert.ToInt64(Scalar("SELECT coalesce(sum(total),0) FROM documents WHERE original=@p0 AND kind IN ('SR','PR') AND date<=@p1 AND (reversal_date IS NULL OR reversal_date>@p1)",id,date));
            long allocated=Convert.ToInt64(Scalar("SELECT coalesce(sum(x.amount),0) FROM allocations x JOIN documents v ON v.id=x.voucher WHERE x.invoice=@p0 AND x.date<=@p1 AND (x.revoked IS NULL OR x.revoked>@p1) AND (v.reversal_date IS NULL OR v.reversal_date>@p1)",id,date));
            long paid=(long)d["paid"]+allocated;result.Add(new(id,(string)d["number"],(string)d["party_name"],(string)d["kind"],(string)d["date"],string.IsNullOrEmpty((string)d["due_date"])?(string)d["date"]:(string)d["due_date"],(long)d["total"],paid,returned,(long)d["total"]-paid-returned));
        }return result;
    }
    public void Allocate(long voucher,long invoice,decimal amount,DateTime date)
    {
        if(D(M(amount))!=amount)throw new InvalidOperationException("Allocation precision is limited to 3 decimals");
        store.Require("post");EnsureOpen(date);using var tx=store.BeginTransaction();AllocateInternal(voucher,invoice,M(amount),date);tx.Commit();
    }
    void AllocateInternal(long voucher,long invoice,long amount,DateTime date)
    {
        var v=Table("SELECT * FROM documents WHERE id=@p0",voucher).Rows[0];var inv=Table("SELECT * FROM documents WHERE id=@p0",invoice).Rows[0];
        if(amount<=0||(long)v["reversed"]==1||(long)inv["reversed"]==1||!((v["kind"].ToString() is "RC" or "CR"&&inv["kind"].ToString()=="SI")||(v["kind"].ToString() is "PV" or "CP"&&inv["kind"].ToString()=="PI"))||v["party"].ToString()!=inv["party"].ToString()||date.Date<DateTime.Parse((string)v["date"])||date.Date<DateTime.Parse((string)inv["date"])||date>DateTime.Today)throw new InvalidOperationException("تحقق من نوع السند والفاتورة والحساب والتاريخ / Check voucher, invoice, party and allocation date");
        long available=(long)v["total"]-Convert.ToInt64(Scalar("SELECT coalesce(sum(amount),0) FROM allocations WHERE voucher=@p0 AND revoked IS NULL",voucher));
        var position=Positions(DateTime.Today).Single(p=>p.Invoice==invoice);
        if(amount>available||amount>position.Outstanding)throw new InvalidOperationException("المبلغ يتجاوز المتاح في السند أو المتبقي على الفاتورة / Allocation exceeds voucher funds or invoice balance");
        Exec("INSERT INTO allocations(voucher,invoice,amount,date) VALUES(@p0,@p1,@p2,@p3)",voucher,invoice,amount,date.ToString("yyyy-MM-dd"));Audit("allocate",voucher+" → "+invoice+": "+Money(amount));
    }
    public void RemoveAllocation(long id,DateTime date)
    {
        store.Require("post");EnsureOpen(date);var allocation=Table("SELECT * FROM allocations WHERE id=@p0",id).Rows[0];if(date.Date>DateTime.Today||date.Date<DateTime.Parse((string)allocation["date"]))throw new InvalidOperationException("Invalid removal date");using var tx=store.BeginTransaction();Exec("UPDATE allocations SET revoked=@p0 WHERE id=@p1 AND revoked IS NULL",date.ToString("yyyy-MM-dd"),id);Audit("unallocate",id.ToString());tx.Commit();
    }
    public void SetDueDate(long invoice,DateTime due)
    {
        store.Require("post");var doc=Table("SELECT * FROM documents WHERE id=@p0",invoice).Rows[0];EnsureOpen(DateTime.Parse((string)doc["date"]));if(due.Date<DateTime.Parse((string)doc["date"]))throw new InvalidOperationException("الاستحقاق قبل الفاتورة / Due date precedes invoice");Exec("UPDATE documents SET due_date=@p0 WHERE id=@p1",due.ToString("yyyy-MM-dd"),invoice);Audit("due-date",invoice+" "+due.ToString("yyyy-MM-dd"));
    }
    public long ReturnInvoice(long original,DateTime date,List<ReturnLine> requested,string reason)
    {
        if(requested.Count==0||string.IsNullOrWhiteSpace(reason)||requested.Select(r=>r.SourceLine).Distinct().Count()!=requested.Count)throw new InvalidOperationException("حدد الكميات وسبب المرتجع / Select return quantities and reason");
        using var tx=store.BeginTransaction();var source=Table("SELECT * FROM documents WHERE id=@p0",original).Rows[0];string sourceKind=(string)source["kind"];
        if(sourceKind is not("SI" or "PI")||(long)source["reversed"]==1||date.Date<DateTime.Parse((string)source["date"]))throw new InvalidOperationException("الفاتورة أو تاريخ المرتجع غير صالح / Invalid invoice or return date");
        long net=0,tax=0;var calculated=new List<(DataRow Line,long Qty,long Net,long Tax,long Discount)>();
        foreach(var request in requested){var line=Table("SELECT * FROM invoice_lines WHERE id=@p0 AND doc=@p1",request.SourceLine,original);if(line.Rows.Count==0)throw new InvalidOperationException("Invalid source line");var l=line.Rows[0];long q=M(request.Quantity);
            var old=Table("SELECT coalesce(sum(l.qty),0) qty,coalesce(sum(l.net),0) net,coalesce(sum(l.tax),0) tax,coalesce(sum(l.discount),0) discount FROM invoice_lines l JOIN documents d ON d.id=l.doc WHERE l.source_line=@p0 AND d.reversed=0",request.SourceLine).Rows[0];
            long remaining=(long)l["qty"]-Convert.ToInt64(old["qty"]);if(q<=0||D(q)!=request.Quantity||q>remaining)throw new InvalidOperationException("كمية المرتجع تتجاوز المتبقي / Return quantity exceeds unreturned quantity");
            long Portion(string field)=>q==remaining?(long)l[field]-Convert.ToInt64(old[field]):M(D((long)l[field])*q/(long)l["qty"]);
            long n=Portion("net"),t=Portion("tax");net+=n;tax+=t;calculated.Add((l,q,n,t,Portion("discount")));
        }
        long id=NewDoc(sourceKind=="SI"?"SR":"PR",date,(long)source["party"],null,reason,new(net,tax,net+tax),0,decimal.Parse((string)source["vat"],CultureInfo.InvariantCulture),(long)source["inclusive"]==1);
        Exec("UPDATE documents SET original=@p0,branding=@p1,logo=@p2,seal=@p3 WHERE id=@p4",original,source["branding"],source["logo"],source["seal"],id);
        foreach(var l in calculated)Exec("INSERT INTO invoice_lines(doc,item,description,qty,price,discount,net,tax,cost,source_line) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,0,@p8)",id,l.Line["item"],l.Line["description"],l.Qty,l.Line["price"],l.Discount,l.Net,l.Tax,l.Line["id"]);
        RebuildInventory("return "+id);Audit("return",source["number"]+" → "+id);tx.Commit();return id;
    }
    sealed class StockState {public long Qty;public long Value;public decimal LastCost;}
    public void RebuildInventory(string reason)
    {
        if(deferredRebuild>0)return;
        var states=Items().Where(i=>i.Stock).ToDictionary(i=>i.Id,i=>new StockState());
        var docs=Table("SELECT * FROM documents WHERE kind IN ('SI','PI','SR','PR','OB','VA') AND reversed=0 ORDER BY date,id");
        foreach(DataRow doc in docs.Rows){long id=(long)doc["id"];string kind=(string)doc["kind"];long goods=0,services=0,cost=0,variance=0,oldCost=Convert.ToInt64(Scalar("SELECT coalesce(sum(cost),0) FROM invoice_lines WHERE doc=@p0",id));
            if(kind is "OB" or "VA"){
                foreach(DataRow move in Table("SELECT * FROM stock_moves WHERE doc=@p0",id).Rows){var st=states[(long)move["item"]];st.Qty+=(long)move["qty"];st.Value+=(long)move["value"];if(st.Qty>0)st.LastCost=D(st.Value)*1000/st.Qty;}continue;
            }
            Exec("DELETE FROM stock_moves WHERE doc=@p0",id);
            foreach(DataRow line in Table("SELECT * FROM invoice_lines WHERE doc=@p0 ORDER BY id",id).Rows){long item=(long)line["item"],q=(long)line["qty"],n=(long)line["net"],lc=0;bool stock=states.TryGetValue(item,out var st);
                if(stock){
                    bool incoming=kind is "PI" or "SR";
                    if(incoming){
                        if(kind=="PI")lc=n;else{var source=Table("SELECT qty,cost FROM invoice_lines WHERE id=@p0",line["source_line"]).Rows[0];lc=M(D((long)source["cost"])*q/(long)source["qty"]);}
                        long beforeQty=st!.Qty,beforeValue=st.Value;st.Qty+=q;st.Value+=lc;
                        if(beforeQty<0){long cover=Math.Min(q,-beforeQty);long provisional=cover==-beforeQty?-beforeValue:M(D(-beforeValue)*cover/-beforeQty);long actual=M(D(lc)*cover/q);long adjustment=actual-provisional;st.Value-=adjustment;variance+=adjustment;}
                        if(st.Qty>0)st.LastCost=D(st.Value)*1000/st.Qty;else if(st.Qty==0){variance+=st.Value;st.Value=0;}
                        Exec("INSERT INTO stock_moves(doc,item,qty,value) VALUES(@p0,@p1,@p2,@p3)",id,item,q,st.Value-beforeValue);
                    }else{
                        decimal average=st!.Qty>0?D(st.Value)*1000/st.Qty:st.LastCost;
                        lc=q==st.Qty&&st.Qty>0?st.Value:M(average*D(q));long before=st.Value;st.Qty-=q;st.Value-=lc;
                        if(st.Qty<0&&store.Setting("allow_negative","0")!="1")throw new InvalidOperationException("الكمية غير كافية في التسلسل التاريخي / Insufficient stock in date order: "+Items().Single(i=>i.Id==item).Name);
                        Exec("INSERT INTO stock_moves(doc,item,qty,value) VALUES(@p0,@p1,@p2,@p3)",id,item,-q,st.Value-before);
                    }
                    goods+=n;cost+=lc;
                }else services+=n;
                Exec("UPDATE invoice_lines SET cost=@p0 WHERE id=@p1",lc,line["id"]);
            }
            Exec("DELETE FROM entries WHERE doc=@p0",id);long total=(long)doc["total"],tax=(long)doc["tax"],net=(long)doc["net"],paid=(long)doc["paid"],party=(long)doc["party"];var jl=new List<EntryLine>();
            void Signed(long account,long debit){if(debit>0)jl.Add(new(account,debit,0));else if(debit<0)jl.Add(new(account,0,-debit));}
            if(kind=="SI"){Signed(party,total-paid);if(paid>0)Signed((long)doc["cash"],paid);Signed(AccountId("4101"),-net);Signed(AccountId("2102"),-tax);Signed(AccountId("5104"),cost);Signed(AccountId("1301"),-cost);}
            if(kind=="PI"){Signed(AccountId("1301"),goods-variance);Signed(AccountId("5104"),variance);Signed(AccountId("5101"),services);Signed(AccountId("1401"),tax);Signed(party,-(total-paid));if(paid>0)Signed((long)doc["cash"],-paid);}
            if(kind=="SR"){Signed(party,-total);Signed(AccountId("4101"),net);Signed(AccountId("2102"),tax);Signed(AccountId("1301"),cost-variance);Signed(AccountId("5104"),-cost+variance);}
            if(kind=="PR"){Signed(party,total);Signed(AccountId("1401"),-tax);Signed(AccountId("1301"),-cost);Signed(AccountId("5101"),-services);Signed(AccountId("5104"),cost-goods);}
            if(jl.Any())Journal(id,jl);
            if(oldCost!=cost)Exec("INSERT INTO cost_history(doc,old_cost,new_cost,reason) VALUES(@p0,@p1,@p2,@p3)",id,oldCost,cost,reason);
        }
        foreach(var state in states)Exec("UPDATE items SET qty=@p0,value=@p1,last_cost=@p2 WHERE id=@p3",state.Value.Qty,state.Value.Value,M(state.Value.LastCost),state.Key);
    }
    public long AmendInvoice(long original,bool sale,DateTime date,long party,long? cash,decimal paid,decimal vat,bool inclusive,string note,List<InvoiceLine> lines,string reason)
    {
        if(string.IsNullOrWhiteSpace(reason))throw new InvalidOperationException("سبب التعديل مطلوب / Amendment reason required");
        using var tx=store.BeginTransaction();var d=Table("SELECT * FROM documents WHERE id=@p0",original).Rows[0];if(d["kind"].ToString()!=(sale?"SI":"PI")||(long)d["reversed"]==1)throw new InvalidOperationException("Invalid invoice for amendment");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM documents WHERE original=@p0 AND kind IN ('SR','PR') AND reversed=0",original))>0)throw new InvalidOperationException("ألغِ المرتجعات المرتبطة قبل تعديل الفاتورة / Cancel linked returns before amending this invoice");
        string snapshot=JsonSerializer.Serialize(new{header=d.Table.Columns.Cast<DataColumn>().ToDictionary(c=>c.ColumnName,c=>d[c]==DBNull.Value?null:d[c]),lines=Table("SELECT * FROM invoice_lines WHERE doc=@p0",original).Rows.Cast<DataRow>().Select(r=>r.Table.Columns.Cast<DataColumn>().ToDictionary(c=>c.ColumnName,c=>r[c]))});
        var allocations=Table("SELECT * FROM allocations WHERE invoice=@p0 AND revoked IS NULL",original);
        // Reverse and replace as one transaction; originals and the reason remain visible.
        long replacement;deferredRebuild++;try{Reverse(original,reason,DateTime.Parse((string)d["date"]));replacement=Invoice(sale,date,party,cash,paid,vat,inclusive,note,lines);}finally{deferredRebuild--;}RebuildInventory("amend "+original);
        foreach(DataRow a in allocations.Rows)if(party==(long)d["party"]){long available=Positions(DateTime.Today).Single(p=>p.Invoice==replacement).Outstanding;long amount=Math.Min((long)a["amount"],Math.Max(0,available));if(amount>0)AllocateInternal((long)a["voucher"],replacement,amount,DateTime.Parse((string)a["date"])<date?date:DateTime.Parse((string)a["date"]));}
        Exec("INSERT INTO attachments(doc,name,data) SELECT @p0,name,data FROM attachments WHERE doc=@p1",replacement,original);Exec("UPDATE documents SET replaced_by=@p0 WHERE id=@p1",replacement,original);Exec("INSERT INTO revisions(doc,replacement,reason,snapshot,username) VALUES(@p0,@p1,@p2,@p3,@p4)",original,replacement,reason,snapshot,store.User.Username);Audit("amend",original+" → "+replacement+": "+reason);tx.Commit();return replacement;
    }
    public long RegisterCheque(bool incoming,string number,string bank,long party,decimal amount,DateTime issue,DateTime due,long? invoice=null)
    {
        if(party==AccountId("1301"))throw new InvalidOperationException("Use invoices for inventory");
        long value=M(amount);if(value<=0||D(value)!=amount||string.IsNullOrWhiteSpace(number)||string.IsNullOrWhiteSpace(bank)||due.Date<issue.Date)throw new InvalidOperationException("أكمل بيانات الشيك والمبلغ والتاريخ / Complete cheque number, bank, amount and dates");
        using var tx=store.BeginTransaction();long id=NewDoc(incoming?"CR":"CP",issue,party,null,number+" · "+bank,new(value,0,value));Journal(id,incoming?[new(AccountId("1104"),value,0),new(party,0,value)]:[new(party,value,0),new(AccountId("2190"),0,value)]);
        Exec("UPDATE documents SET due_date=@p0 WHERE id=@p1",due.ToString("yyyy-MM-dd"),id);
        Exec("INSERT INTO cheques(number,bank,direction,party,amount,issue_date,due_date,issue_doc) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",number.Trim(),bank.Trim(),incoming?"Incoming":"Outgoing",party,value,issue.ToString("yyyy-MM-dd"),due.ToString("yyyy-MM-dd"),id);if(invoice!=null)AllocateInternal(id,invoice.Value,Math.Min(value,Positions(DateTime.Today).Single(p=>p.Invoice==invoice).Outstanding),issue);tx.Commit();return id;
    }
    public long SettleCheque(long cheque,string state,long bankAccount,DateTime date,string reason)
    {
        using var tx=store.BeginTransaction();var c=Table("SELECT * FROM cheques WHERE id=@p0",cheque).Rows[0];string previous=(string)c["state"];if(state is not("Cleared" or "Bounced" or "Cancelled")||previous is "Bounced" or "Cancelled"||(previous=="Cleared"&&state=="Cleared")||date.Date<DateTime.Parse((string)c["issue_date"]))throw new InvalidOperationException("Invalid cheque transition");
        bool incoming=c["direction"].ToString()=="Incoming";long amount=(long)c["amount"],doc;
        if(state=="Cleared"){
            if(Accounts().Single(a=>a.Id==bankAccount).Kind!="Cash")throw new InvalidOperationException("Choose a bank account");doc=NewDoc("CC",date,(long)c["party"],bankAccount,reason,new(amount,0,amount));Journal(doc,incoming?[new(bankAccount,amount,0),new(AccountId("1104"),0,amount)]:[new(AccountId("2190"),amount,0),new(bankAccount,0,amount)]);
        }else{
            if(string.IsNullOrWhiteSpace(reason))throw new InvalidOperationException("سبب الإرجاع أو الإلغاء مطلوب / Reason required");
            if(previous=="Cleared")Reverse((long)c["settlement_doc"],reason,date,true);doc=Reverse((long)c["issue_doc"],reason,date,true);
        }
        Exec("UPDATE cheques SET state=@p0,settlement_doc=@p1 WHERE id=@p2",state,doc,cheque);Audit("cheque-state",cheque+" "+state);tx.Commit();return doc;
    }
    public long CloseYear(DateTime start,DateTime end)
    {
        store.Require("period");if(start>end||end.Date>=DateTime.Today||Convert.ToInt64(Scalar("SELECT count(*) FROM periods WHERE NOT(end<@p0 OR start>@p1)",start.ToString("yyyy-MM-dd"),end.ToString("yyyy-MM-dd")))>0)throw new InvalidOperationException("الفترة غير صالحة أو تتداخل مع فترة مقفلة / Invalid or overlapping period");
        using var tx=store.BeginTransaction();var lines=new List<EntryLine>();long net=0;
        foreach(DataRow r in Table("SELECT a.id,sum(e.debit-e.credit) balance FROM accounts a JOIN entries e ON e.account=a.id JOIN documents d ON d.id=e.doc WHERE a.kind IN ('Income','Expense') AND d.date BETWEEN @p0 AND @p1 GROUP BY a.id",start.ToString("yyyy-MM-dd"),end.ToString("yyyy-MM-dd")).Rows){long b=(long)r["balance"];net+=b;if(b>0)lines.Add(new((long)r["id"],0,b));else if(b<0)lines.Add(new((long)r["id"],-b,0));}
        if(net>0)lines.Add(new(AccountId("3301"),net,0));else if(net<0)lines.Add(new(AccountId("3301"),0,-net));
        long id=NewDoc("CL",end,null,null,"Year closing",new(lines.Sum(l=>l.Debit),0,lines.Sum(l=>l.Debit)));if(lines.Count>0)Journal(id,lines);Exec("INSERT INTO periods(start,end,closing_doc) VALUES(@p0,@p1,@p2)",start.ToString("yyyy-MM-dd"),end.ToString("yyyy-MM-dd"),id);Audit("close-period",start+" → "+end);tx.Commit();return id;
    }
    public long AmendSimple(long original,DateTime date,long? party,long? cash,decimal amount,string note,List<EntryLine>? lines,string reason)
    {
        if(string.IsNullOrWhiteSpace(reason))throw new InvalidOperationException("سبب التعديل مطلوب / Reason required");using var tx=store.BeginTransaction();var source=Table("SELECT * FROM documents WHERE id=@p0",original).Rows[0];string kind=(string)source["kind"];if(kind is not("RC" or "PV" or "JV"))throw new InvalidOperationException("Unsupported document amendment");
        string snapshot=JsonSerializer.Serialize(source.Table.Columns.Cast<DataColumn>().ToDictionary(c=>c.ColumnName,c=>source[c]==DBNull.Value?null:source[c]));var allocations=Table("SELECT * FROM allocations WHERE voucher=@p0 AND revoked IS NULL",original);Reverse(original,reason,DateTime.Parse((string)source["date"]));long replacement=kind=="JV"?Manual(date,note,lines!):Voucher(kind=="RC",date,party!.Value,cash!.Value,amount,note);
        foreach(DataRow allocation in allocations.Rows){long available=(long)Table("SELECT total FROM documents WHERE id=@p0",replacement).Rows[0]["total"]-Convert.ToInt64(Scalar("SELECT coalesce(sum(amount),0) FROM allocations WHERE voucher=@p0 AND revoked IS NULL",replacement));var pos=Positions(DateTime.Today).FirstOrDefault(p=>p.Invoice==(long)allocation["invoice"]);if(pos!=null&&party?.ToString()==source["party"].ToString()){long value=Math.Min((long)allocation["amount"],Math.Min(available,Math.Max(0,pos.Outstanding)));if(value>0)AllocateInternal(replacement,pos.Invoice,value,DateTime.Parse((string)allocation["date"])<date?date:DateTime.Parse((string)allocation["date"]));}}
        Exec("UPDATE documents SET replaced_by=@p0 WHERE id=@p1",replacement,original);Exec("INSERT INTO revisions(doc,replacement,reason,snapshot,username) VALUES(@p0,@p1,@p2,@p3,@p4)",original,replacement,reason,snapshot,store.User.Username);tx.Commit();return replacement;
    }
    public long RevalueStock(long item,decimal unitCost,string reason)
    {
        if(unitCost<0||D(M(unitCost))!=unitCost||string.IsNullOrWhiteSpace(reason))throw new InvalidOperationException("تكلفة صالحة وسبب مطلوبان / Valid cost and reason required");var current=Items().Single(i=>i.Id==item);if(!current.Stock||current.Qty<=0)throw new InvalidOperationException("اختر صنفًا مخزنيًا بكمية موجبة / Choose a stock item with positive quantity");long delta=M(D(current.Qty)*unitCost)-current.Value;if(delta==0)throw new InvalidOperationException("التكلفة لم تتغير / Cost is unchanged");using var tx=store.BeginTransaction();long id=NewDoc("VA",DateTime.Today,null,null,reason,new(Math.Abs(delta),0,Math.Abs(delta)));Exec("INSERT INTO stock_moves(doc,item,qty,value) VALUES(@p0,@p1,0,@p2)",id,item,delta);Journal(id,delta>0?[new(AccountId("1301"),delta,0),new(AccountId("5104"),0,delta)]:[new(AccountId("5104"),-delta,0),new(AccountId("1301"),0,-delta)]);RebuildInventory("stock revaluation "+id);Audit("revalue",item+": "+unitCost);tx.Commit();return id;
    }
    public void ReopenYear(long period,string reason)
    {
        store.Require("period");if(string.IsNullOrWhiteSpace(reason))throw new InvalidOperationException("Reason required");using var tx=store.BeginTransaction();var p=Table("SELECT * FROM periods WHERE id=@p0",period).Rows[0];if(Convert.ToInt64(Scalar("SELECT count(*) FROM periods WHERE end>@p0",p["end"]))>0)throw new InvalidOperationException("أعد فتح الفترات الأحدث أولًا / Reopen newer periods first");
        Exec("DELETE FROM periods WHERE id=@p0",period);long closing=(long)p["closing_doc"];if(Convert.ToInt64(Scalar("SELECT count(*) FROM entries WHERE doc=@p0",closing))>0)Reverse(closing,reason,DateTime.Parse((string)p["end"]),true);else Exec("UPDATE documents SET reversed=1,reversal_date=@p0 WHERE id=@p1",((string)p["end"]),closing);Audit("reopen-period",reason);tx.Commit();
    }
    public DataTable TaxEvents(DateTime from,DateTime to)
    {
        return Table("""
        SELECT d.date,d.number,d.kind,d.net,d.tax,d.total,CASE WHEN d.kind IN ('SR','PR') THEN -1 ELSE 1 END sign
        FROM documents d WHERE d.kind IN ('SI','PI','SR','PR') AND d.date BETWEEN @p0 AND @p1
        UNION ALL
        SELECT r.date,r.number,s.kind,s.net,s.tax,s.total,CASE WHEN s.kind IN ('SR','PR') THEN 1 ELSE -1 END sign
        FROM documents r JOIN documents s ON s.id=r.original WHERE r.kind='RV' AND s.kind IN ('SI','PI','SR','PR') AND r.date BETWEEN @p0 AND @p1 ORDER BY date,number
        """,from.ToString("yyyy-MM-dd"),to.ToString("yyyy-MM-dd"));
    }
}

