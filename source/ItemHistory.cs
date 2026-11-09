using System.Data;
namespace Hisab;

public record ItemHistoryEntry(long Id,string Source,DateTime Date,string Number,string Kind,long Location,long? Party,long Incoming,long Outgoing,long DocumentQuantity,string State,string Serials,string Note,long EffectiveChange);
public sealed partial class Store
{
    // One row per document/location. Reversals inherit the original locations: legacy
    // reversal stock_moves do not retain location, and must not be used for this view.
    public List<ItemHistoryEntry> ItemHistory(long item)
    {
        var serials=Table("SELECT e.doc,e.location,u.number FROM serial_events e JOIN serial_units u ON u.id=e.unit WHERE u.item=@p0 ORDER BY e.id",item).Rows.Cast<DataRow>().GroupBy(r=>((long)r["doc"],(long)r["location"])).ToDictionary(g=>g.Key,g=>string.Join("\n",g.Select(r=>(string)r["number"]).Distinct()));
        var rows=Table("""
        WITH keys AS (
          SELECT m.doc,m.location FROM stock_moves m JOIN documents d ON d.id=m.doc WHERE m.item=@p0 AND d.kind<>'RV'
          UNION SELECT doc,location FROM invoice_lines WHERE item=@p0
          UNION SELECT e.doc,e.location FROM serial_events e JOIN serial_units u ON u.id=e.unit WHERE u.item=@p0
          UNION SELECT a.doc,l.location FROM landed_allocations a JOIN invoice_lines l ON l.id=a.receipt_line WHERE l.item=@p0
        ), events AS (
          SELECT doc,location,doc data_doc FROM keys
          UNION SELECT d.id,k.location,k.doc FROM documents d JOIN keys k ON k.doc=d.original WHERE d.kind='RV'
        ), moves AS (
          SELECT doc,location,sum(CASE WHEN qty>0 THEN qty ELSE 0 END) incoming,sum(CASE WHEN qty<0 THEN -qty ELSE 0 END) outgoing FROM stock_moves WHERE item=@p0 GROUP BY doc,location
        ), lines AS (SELECT doc,location,sum(qty) quantity FROM invoice_lines WHERE item=@p0 GROUP BY doc,location)
        SELECT d.id,d.number,d.kind,d.date,d.party,d.note,d.reversed,e.location,e.data_doc,
          coalesce(m.incoming,0) incoming,coalesce(m.outgoing,0) outgoing,coalesce(l.quantity,max(m.incoming,m.outgoing),0) quantity
        FROM events e JOIN documents d ON d.id=e.doc
        LEFT JOIN moves m ON m.doc=e.data_doc AND m.location=e.location
        LEFT JOIN lines l ON l.doc=e.data_doc AND l.location=e.location
        ORDER BY d.date DESC,d.id DESC,e.location
        """,item);
        var result=new List<ItemHistoryEntry>();
        foreach(DataRow r in rows.Rows){long id=(long)r["id"],loc=(long)r["location"],incoming=(long)r["incoming"],outgoing=(long)r["outgoing"];string kind=(string)r["kind"];bool reversed=(long)r["reversed"]==1;if(kind=="RV")(incoming,outgoing)=(outgoing,incoming);result.Add(new(id,"document",DateTime.Parse((string)r["date"]),(string)r["number"],kind,loc,r["party"]==DBNull.Value?null:(long)r["party"],incoming,outgoing,(long)r["quantity"],reversed?"Cancelled":kind=="RV"?"Reversal":"Posted",serials.GetValueOrDefault(((long)r["data_doc"],loc),""),(string)r["note"],reversed||kind=="RV"?0:incoming-outgoing));}
        foreach(DataRow r in Table("SELECT r.*,l.location,l.qty,l.serials FROM reservations r JOIN reservation_lines l ON l.reservation=r.id WHERE l.item=@p0",item).Rows){string state=(string)r["state"];var expires=DateTime.Parse((string)r["expires"]);if(state=="Active"&&expires.Date<DateTime.Today)state="Expired";long id=(long)r["id"];result.Add(new(id,"reservation",DateTime.Parse((string)r["created"]),"RS-"+id.ToString("D6"),"Reservation",(long)r["location"],(long)r["customer"],0,0,(long)r["qty"],state,(string)r["serials"],(string)r["note"],0));}
        return result.OrderByDescending(r=>r.Date).ThenByDescending(r=>r.Id).ThenBy(r=>r.Source,StringComparer.Ordinal).ThenBy(r=>r.Location).ToList();
    }
    public static List<ItemHistoryEntry> FilterItemHistory(IEnumerable<ItemHistoryEntry> rows,DateTime? from=null,DateTime? to=null,long location=0,string kind="",string search="")
    {
        if(from?.Date>to?.Date)throw new InvalidOperationException("راجع نطاق التاريخ / Start date must not follow end date");
        search=search.Trim();return rows.Where(r=>(from==null||r.Date>=from.Value.Date)&&(to==null||r.Date<=to.Value.Date)&&(location==0||r.Location==location)&&(kind.Length==0||r.Kind==kind)&&(search.Length==0||(r.Number+" "+r.Serials+" "+r.Note).Contains(search,StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
