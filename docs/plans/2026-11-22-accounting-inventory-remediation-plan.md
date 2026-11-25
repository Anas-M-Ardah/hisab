# Hisab: accounting and inventory audit remediation plan

Date: 2026-11-22. Baseline: v0.11.0, commit `a2899dd28af26fc4b25530ab64127cf772b45c8f`, schema 7.

Implementation update, November 22: the user subsequently authorized proceeding. The isolated runner and ACC-01/ACC-02 fixes are implemented locally and independently reviewed. Full regression: 447 passing assertions; final targeted run: 61 passing assertions, including four added guard checks after the full run. See [milestones 0–1 evidence](../audits/2026-11-22-remediation/README.md). Remaining milestones are pending; no release or production-data repair has occurred. The planning-only statements below describe the original plan creation.

Second batch, November 22: implemented return remainder handling, contiguous closing controls, bounded equal-cost closed-receipt billing, party reconciliation visibility, and grouped numeric input. Final full suite: 493 passing checks, zero failures; Arabic/English report and print previews verified. [Milestone 2 scope and evidence](../audits/2026-11-22-remediation/MILESTONE-2.md) distinguish these delivered changes from pending detailed opening documents, late price-variance treatment, and broader closing/costing policies. Milestone 2 is partially complete, not fully closed.

Implementation update, November 24: detailed customer/supplier openings and explicit current-period allocation of late receipt price differences are implemented and verified (see [opening/late-billing evidence](../audits/2026-11-23-openings-late-billing/README.md)). Comparative P&L, balance sheet, account mappings, historical stock valuation and unbilled receipt reconciliation are now implemented locally. Final full suite: 590 passing checks, zero failures; 24 bilingual UI/print checks. See [financial reporting evidence](../audits/2026-11-24-financial-reports/README.md). Milestone 3 remains partial: cash-flow reporting is outstanding. Bank reconciliation, schedules and the complete linked-month acceptance exercise remain on the roadmap.
Cash-flow implementation update, November 24: the direct-method report, transaction detail and explicit split classifications are implemented locally. Full synthetic suite: 618 passing checks, zero failures. This replaces the initial indirect-method proposal because actual cash ledger movements are reliable while noncash adjustments require additional schedules. See [cash-flow evidence and contract](../audits/2026-11-24-cash-flow/README.md). Noncash disclosures and the linked-month acceptance exercise remain open.

Reporting completeness update, November 25: added explicit journal-linked noncash disclosures, immutable report-content snapshots with integrity checks, and shared displayed-order capture for snapshot/print/CSV. Full synthetic regression: 640 passing checks, zero failures. See [reporting completeness evidence](../audits/2026-11-25-reporting-completeness/README.md). The noncash register requires accountant identification and explanation; it does not automatically establish completeness. Bank reconciliation, schedules and the linked-month acceptance exercise remain outstanding.

## القرار التنفيذي — Executive decision

حساب يحتوي على وظائف مهمة للمبيعات والمشتريات والمخزون والضمان، لكنه يحتاج إصلاح العيوب المالية المثبتة واستكمال التسويات والقوائم قبل وصفه كنظام محاسبي متكامل ومدقق. الأولوية لصحة الذمم والفترات والتكلفة، ثم اكتمال التقارير وسهولة التشغيل بالعربية.

Deliver this in small reviewed milestones. Do not add new modules on top of unresolved financial defects. Preserve the offline Windows application, Arabic/English support, three-decimal JOD, existing data, and the user's requirement for no external business-system integrations. Free warranty replacements remain the supported exchange scope.

This is an implementation plan, not authorization to change live data or publish a release. No production fixes were made during this planning task.

## Audit basis and limitations

- [Arabic audit](../audits/2026-11-19/AUDIT-AR.md) and [evidence guide](../audits/2026-11-19/EVIDENCE.md) contain reproductions and source references.
- On November 22, HEAD and an empty source diff against the audited commit confirmed that production source remains unchanged. Allocation, return-cost, and receipt-billing code was checked against the recorded findings. This is a consolidation of the existing audit, not a newly executed full audit.
- Runtime accounting/inventory evidence dates from November 19; the parser probe was rerun November 22. The existing regression run passed 390 checks, but independent probes exposed defects outside those checks.
- Three specialist reviews covered seven logical accounting cases, four inventory flows, eight numeric inputs, and six existing UI images. Blind calibration classified seven of eight cases exactly, detecting all four seeded defects without marking a clean case defective.
- Thirty independent scenarios and a complete business-month simulation remain to be executed. Permissions, recovery, migrations, broad UI parity, physical printing, and company-specific tax applicability are not approved by this baseline.
- The audit report documents an accidental normal-app startup that touched the live database file. No business transactions were entered through that launch; no before-snapshot exists to prove unchanged contents. Future harnesses must enforce the isolation gate below.

## Findings and capability register

| ID | Classification | Business consequence | Milestone |
|---|---|---|---|
| ACC-01 | Confirmed P1 defect | One JOD 100 payment appears to settle JOD 200 historically after revocation and backdated reallocation; reproduced for customers and suppliers | 1 |
| ACC-02 | Confirmed P1 blocked workflow | February bounce of a January cheque cannot be recorded after January closes; customer remains settled | 1 |
| INV-01 | Confirmed P2 rounding defect | Three partial returns restore 99.999 of an original 100.000 cost, leaving 0.001 COGS | 2 |
| INV-02 | Confirmed P2 workflow gap | Current-period supplier bill cannot settle a receipt from a closed period, even at the same price | 2 |
| ACC-03 | P2 control/policy gap | Closing February can lock January without including January results in the closing entry | 2 |
| CAP-01 | Confirmed P2 reconciliation gap | Manual customer opening balance appears in GL but not invoice positions; supplier equivalent needs a dedicated reproduction | 2 |
| CAP-02 | P2 capability gap | No dedicated balance sheet or cash-flow statement in the assessed report path; P&L lacks useful classification | 3 |
| CAP-03 | P2 report capability/clarity gap | Current inventory report ignores dates while retaining a period header; historical valuation needs a separate contract | 3 |
| AR-01 | Confirmed P2 input defect | Formatted Arabic/Western thousands-separated amounts cannot be parsed back | 2 |
| INV-OBS-03 | Policy-dependent observed behavior | Later landed costs restate earlier open-period valuation and COGS; no closed-period mutation demonstrated | 2–3 |
| AR-03 / AR-04 | Source concern / visual polish | Translation coverage and large-font warranty columns need runtime verification | 4 |
| Bank reconciliation; fixed assets | Candidate capability gaps | No dedicated workflow found in the limited review; verify existing routes before designing modules | 4–5 |

Existing quotes, reservations, locations, item history, Excel preview import, serial tracking, and warranty custody should be extended and regression-tested, not rebuilt. Payroll, manufacturing, consolidation, and enterprise-scale multiuser deployment are outside this plan. Foreign currency is a separate decision if the company's purchasing requires it.

## Milestone 0 — Reliable audit and regression harness

1. Build a uniquely named audit executable from a clean output directory, excluding copied `bin`/`obj`. Set and verify the explicit test entrypoint and synthetic database path before execution. Fail immediately if the database path resolves into the live Hisab data directory.
2. Turn the confirmed reproductions into independent expected-value assertions. A probe completing with exit code zero is not a passing business test; assertions must fail for incorrect balances and blocked required workflows.
3. Retain fixture inputs, expected journal entries, actual output, source commit, schema, and execution command. Use integer thousandths/decimal arithmetic and an independently calculated oracle.

Exit: isolated tests reproduce the known failures on this baseline, preserve the positive warranty/reservation cases, and cannot invoke normal startup. Retain the current passing regression suite as a separate gate.

## Milestone 1 — Correct historical payments and cheque bounces

### ACC-01: temporal allocation controls

Define an allocation's effective interval, including its revocation and voucher reversal dates. Validate voucher capacity and invoice capacity across every affected interval, not merely today's active rows. Prevent a newly backdated allocation from using funds that were still allocated at that historical date. Define same-day boundaries explicitly and preserve audit history.

Acceptance: two invoices of 100.000 and one voucher of 100.000 must never produce 200.000 allocated at any cutoff. Reproduce customer and supplier cases before, on, and after revocation; test partial allocations, backdating, voucher reversal, invoice returns, and a permitted later reallocation. Positions must reconcile to the appropriate GL and separately disclosed credits/advances.

### ACC-02: date-correct cheque bounce posting

Introduce a specific current-period bounce event rather than bypassing the generic closed-period reversal guard. For the reproduced incoming uncleared cheque, post Dr customer 100.000 / Cr cheques for collection 100.000 in February, release its allocation effective that date, and record the new state atomically. Preserve January entries and balances. Define outgoing and already-cleared paths separately before supporting them.

Acceptance: January remains settled; February shows the reopened 100.000 debt and bounced state. Duplicate bounce attempts cannot double-post. Test transaction rollback, permissions, open-period behavior, and supported cancellation paths. Unimplemented state transitions must be rejected clearly.

Exit: both P1 reproductions pass, neighboring payment/cheque flows remain correct, and an independent reviewer checks journals, allocations, state, and cutoff reports together.

## Milestone 2 — Closing, opening balances, and complete stock cost

1. **Period controls (ACC-03):** document the fiscal-year start and distinguish administrative period locking from year-end profit transfer. Reject unintended gaps or explain explicitly which dates become locked. Test the first close, consecutive closes, year-end, reopen, and transactions exactly on boundaries. A February-only action must not silently lock unprocessed January results.
2. **Return cost (INV-01):** allocate original cost across active returns and give the final returned quantity the remaining cost. Ensure deterministic replay, cancellation, and re-return. All three units returned must restore exactly 100.000 and leave zero related COGS.
3. **Closed receipt billing (INV-02):** permit an open-period supplier bill against a closed receipt without rewriting the receipt. The equal-price fixture must post Dr GRNI 200.000 / Cr supplier 200.000 and clear unbilled quantity. Specify current-period treatment for price differences, tax, partial billing, and already-sold quantities before implementing those variants. Do not solve this by disabling closed-period checks.
4. **Opening balances (CAP-01):** provide dated opening customer/supplier documents with due dates, references, and allocation support. Reconcile advances, credits, and manual non-invoice entries explicitly rather than hiding them. Imports must have preview, control totals, duplicate protection, and an opening-control-account reconciliation. Prevent duplicate GL postings when introducing detail for balances already recorded.
5. **Numeric input (AR-01):** accept valid Western and Arabic grouping/decimal forms, preserve signs and thousandths, and reject malformed or ambiguous grouping. Replace integer-only wording in decimal validation messages. Test formatter/parser round trips without silently changing existing saved values.
6. **Landed-cost decision:** document whether open-period history may be restated and how costs arriving after close are recognized. Assess both inventory and already-sold quantities; never make a blanket change to all historical costs without this contract.

Exit: opening subledgers reconcile, same-price late billing works, complete returns leave no residual cost, period boundaries are explicit, and existing historical balances remain stable wherever promised.

## Milestone 3 — A coherent financial reporting set

Build a common report contract: date/cutoff, posting status, account classification, sign conventions, opening/movement/closing totals, rounding, and drill-down to source transactions. Validate mappings for custom accounts and show unmapped accounts instead of omitting them.

- **قائمة الدخل — Income statement:** net sales, COGS, gross profit, operating expenses, and net profit, with a comparable prior period.
- **قائمة المركز المالي — Balance sheet:** assets, liabilities, equity, and current-period profit at a selected date; assets equal liabilities plus equity exactly in stored units.
- **قائمة التدفقات النقدية — Cash flows:** implemented first as a documented direct-method report with explicit operating/investing/financing allocations; an explicit journal-linked noncash disclosure register is implemented, with company review required to identify all relevant transactions. Ending cash must reconcile to designated cash/bank GL accounts. Define cheque classification explicitly.
- **Customer/supplier reconciliation:** opening balance, invoices, credits, receipts/payments, allocations, advances, closing balance, and aging. Show differences and their underlying entries.
- **Historical stock valuation:** quantity and value at a chosen date by item/location, reconciled to inventory GL. Separately label the existing current-stock view. Closed historical results must remain stable after later activity.
- **GRNI reconciliation:** receipt, billed quantity/value, pending balance, and linkage to supplier invoices and control-account entries.

Dependencies: milestones 1–2 and agreed costing/cutoff policies. Acceptance: all statements reconcile on the same synthetic month; switching language changes labels only; printed/exported totals match screen totals. Do not hide discrepancies behind a balancing line.

## Milestone 4 — Daily controls and Arabic usability

First inspect existing cash/bank and permissions routes in depth. Fill demonstrated gaps rather than duplicating features.

- Local bank reconciliation: manual or file-loaded statement lines, preview and duplicate detection, matching/split matching, outstanding cheques/deposits, explicit adjustment approval, reconciliation undo/history. No bank API or external integration. Reconciled statement and book balances must agree after identified timing differences.
- Arabic/English parity: company/account/item names, financial terminology, negative balances, three-decimal JOD, RTL text with LTR serials, and readable print output. Verify the suspected translation inconsistencies in running synthetic-data screens.
- Large-font layouts: test the supported font range, grids, dropdown empty states, keyboard navigation, and warranty timestamps. Record visual evidence rather than relying only on source inspection.
- Permissions/audit trail: exercise posting, reversal, close/reopen, import, restore, and role restrictions through both service and UI paths. Verify who/when/reason/source linkage for material changes.

Exit: reconciliation is explainable and reproducible; a clerk can complete the core flows in either language without changing totals or bypassing permissions.

## Milestone 5 — Managed accounting schedules

After capability discovery, add maintained schedules where manual journals are insufficient for the company's needs:

- Fixed assets: acquisition and capitalization, in-service date, useful life/residual value, depreciation, disposal, and GL-linked register. Methods and policies require accounting review; depreciation cannot run twice for the same asset/period.
- Accruals and prepayments: supporting schedule, periodic posting, controlled reversal, remaining balance, and GL tie-out.
- Month/year-end checklist: reconciliations, adjustment review, inventory/GRNI/AR/AP checks, close snapshot, and controlled reopen.
- Tax configuration review: assess item classification, effective dates, exemptions and return/report handling against the company's actual obligations. Consult current official Jordanian sources when implementing; do not assume all items are subject to one rate or claim legal compliance from arithmetic tests.

Exit: schedules reconcile to ledger and survive close/reopen and restore. Optional workflows can be staged after the core accounting release; their absence must remain visible in the capability matrix.

## Policy decision log

| Decision | Planning assumption / required resolution | Blocks |
|---|---|---|
| Fiscal calendar and closing | Explicit configured fiscal start; separate locking from profit transfer; confirm company calendar before rollout | Period control design |
| Costing | Preserve current weighted-average basis; explicitly define rounding and late-cost recognition | Inventory replay/history |
| Opening migration | Preserve original GL; reconcile imported detail without duplicate balances | Opening wizard |
| Warranty accounting | Preserve current expense treatment for the tested flow; assess provisions separately with accountant | Any policy change |
| Cheques | Specify incoming/outgoing and pending/cleared/bounced transitions and accounts | Expanded cheque support |
| Cash flows | Document cash-equivalent and cheque treatment; validate all account mappings | Cash-flow report |
| Tax and depreciation | Company-specific applicability and approved policies, not generic skill defaults | Professional sign-off |
| Foreign currency | Not yet included; assess only if actual purchasing/payment needs require it | Later scope decision |

These decisions do not block the already-confirmed payment and rounding fixes. Resolve each before its dependent implementation rather than asking the user to approve an unspecified broad redesign.

## Independent acceptance suite — 30 planned scenarios

These are planned end-to-end scenarios, not a claim of 30 executed tests. Each includes expected entries and cutoff balances, UI/service results where applicable, and correction paths. Existing probes can supply fixtures but must become assertions.

| # | Scenario | Required result |
|---|---|---|
| 1 | Customer allocation revoked then backdated | No temporal double-use; GL/positions reconcile |
| 2 | Supplier allocation equivalent | Same control for payables |
| 3 | Partial allocations, same-day boundaries, later reuse | Capacity respected at every event date |
| 4 | Voucher reversal with allocations | Historical settlement preserved until reversal |
| 5 | Pending incoming cheque bounced after close | Current-period debt restored; old period unchanged |
| 6 | Cheque state transitions, duplicates, rollback | Only valid atomic transitions; no duplicate entry |
| 7 | Sales tax arithmetic and later reversal | Correct event-period amounts, no claim of legal applicability |
| 8 | First close with an earlier open month | Explicit lock/profit scope; no silent gap |
| 9 | Consecutive close, year-end, reopen | Correct retained earnings and preserved audit history |
| 10 | Customer detailed opening balances | GL, positions, due dates and aging reconcile |
| 11 | Supplier detailed opening balances | Equivalent AP reconciliation |
| 12 | Advances, credits, manual party entries | Reconciliation explains all non-invoice balances |
| 13 | Weighted-average purchase/sale | Independent quantity, cost, margin and GL totals |
| 14 | Full return in three rounded parts | Restore exactly 100.000; related COGS zero |
| 15 | Cancel/re-enter a partial return | Deterministic remaining cost after replay |
| 16 | Closed receipt billed at same cost | Clear GRNI into AP without changing old receipt |
| 17 | Partial/price-variance bill after close | Approved current-period variance treatment |
| 18 | Landed cost before and after cutoff | Agreed historical behavior; closed dates stable |
| 19 | Reserved serial sale/transfer protections | No double commitment or unauthorized movement |
| 20 | Free warranty replacement and undo | Stock/expense correct; customer and original sale unchanged |
| 21 | Location transfer with serials | Unique location; company quantity/value unchanged |
| 22 | Count adjustment and reversal | Stock and GL adjust together with traceable reason |
| 23 | Excel import preview, duplicate, invalid row | Control totals and atomicity; no unintended partial posting |
| 24 | Historical valuation and GRNI reports | Tie to matching GL accounts and cutoff |
| 25 | P&L, balance sheet, cash flows | Cross-report reconciliation and correct mappings |
| 26 | Local bank statement reconciliation | Matches and timing items explain difference; no duplicate import |
| 27 | Asset/depreciation and prepaid schedule | Schedule/GL tie-out; no duplicate period posting |
| 28 | Arabic/English numerals, names, large fonts, print | Same values and readable usable outputs |
| 29 | Permission denial and interrupted posting | No unauthorized or half-posted state |
| 30 | Upgrade, backup, restore, reopened month | Counts, journals, serials, allocations and reports preserved |

Run a linked synthetic month with opening balances, credit/cash trading, receipts and bills across cutoff, advances, returns, cheque bounce, reservations, warranty, transfers, counts, late costs, adjustments, reconciliation and closing. Then add next-month events and recheck the previous closed month. Restore into a fresh isolated database and compare financial/control totals. Scenario coverage is not established by a test count alone.

## Delivery and release gates

For each milestone: agree the behavioral contract, add failing independent assertions for defects, implement a focused change, run relevant regression tests, obtain independent accounting/inventory review, and update the capability/evidence register. Keep schema migrations reversible through verified backup/restore and test upgrades from schema 7. A previously published baseline is not permission to rewrite existing records silently.

Release candidate gates:

1. No unresolved P0/P1 in assessed flows; all milestone-specific acceptance checks pass.
2. Stock/GL, AR/AP, GRNI, cash, and statement reconciliation differences are zero or explicitly supported timing items.
3. Closed-period invariance, atomic failure handling, permissions, migrations and recovery are demonstrated.
4. Arabic and English core workflows and report output are checked with synthetic data.
5. Reviewer reports distinguish tested, missing, deferred and policy-dependent areas. Qualified local accounting review is required for professional policy sign-off, not replaced by the agent.
6. Publish only when the user requests it; describe remaining scope honestly.

**Next implementation batch:** local bank reconciliation, followed by the outstanding accounting schedules. Preserve the outstanding business-month, recovery and policy acceptance gates.
