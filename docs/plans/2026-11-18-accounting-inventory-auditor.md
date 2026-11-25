# Hisab accounting and inventory validation agent

Date: 2026-11-18. Baseline: v0.11.0, commit a2899dd28af26fc4b25530ab64127cf772b45c8f, schema 7.

Status: researched implementation plan. The specialist has not yet been installed or evaluated.

## Goal

Make Hisab a complete accounting and inventory application for a Jordanian hardware trading company. Define completeness as a business being able to establish opening balances, purchase, receive, stock, sell, collect, pay, reconcile, adjust, close a period and produce consistent financial statements with traceable Arabic documents and recoverable data.

Preserve the local/offline application and the user's no-external-integration requirement. Manual processes and local file import/export are valid. Benchmark established ERP workflows without attempting to clone every enterprise module. Payroll calculations, manufacturing, consolidation and network deployment require separate scope decisions. Fixed assets, accruals, prepayments, bank reconciliation and foreign-currency transactions must be assessed explicitly rather than forgotten.

The first output is an evidence-based completeness matrix and prioritized roadmap, before further feature development.

## Initial repository observations

The code contains journals, account hierarchy, trial balance, ledger/daybook, profit and loss, aging, a sales-tax worksheet, sales/purchases, returns, payments/allocations, cheques and period closing. Inventory includes moving weighted-average replay, locations, serials, transfers, stock counts, purchase orders/receipts/billing, landed costs, replenishment, reservations, item history, Excel imports and warranty claims.

Presence in code does not establish business correctness. The 390 released automated checks provide regression evidence, not proof of completeness.

- `source/ReportsAndPrint.cs` exposes no dedicated balance sheet or cash-flow statement in its report selector.
- Its stock report explicitly shows current inventory and ignores the date range. Assess a separate historical valuation report.
- Initial searches found no dedicated bank-reconciliation, fixed-asset/depreciation or foreign-currency workflow. These remain candidate gaps pending full audit.
- `ARCHITECTURE.txt` still describes v0.4/schema 2. Resolve current behavior from source and executable evidence, and flag stale documentation.

## Online skill research

| Candidate | What was verified | Decision |
|---|---|---|
| Anthropic Finance & Accounting plugin | Skills for journal preparation, reconciliation, financial statements and close management | Adapt selected methodologies. Its US GAAP and connector assumptions must not automatically become Hisab's policies or dependencies. |
| Obra Superpowers requesting-code-review | Separate reviewer with requirements and commit range | Adopt independent review of changes; code review alone is insufficient accounting validation. |
| Obra Superpowers verification-before-completion | Fresh evidence before completion assertions | Apply to audit findings, repairs and release readiness. |
| AbdoBasyioni business-analysis | Arabic RTL analysis and reporting on uploaded spreadsheets | Optional reporting reference, not the core posting/inventory auditor. |
| Community inventory-accounting listing | Search surfaced a specialized inventory review skill | Not selected: attempted primary source path returned 404. Marketplace labels do not establish competence. |

No reviewed candidate establishes Arabic, Jordanian accounting, inventory and Hisab-specific implementation competence together. Build a focused custom skill, `hisab-accounting-inventory-auditor`, rather than install a broad collection of prompts.

Before adopting external files, inspect source and helpers, verify licenses, preserve required attribution, and record repository/path/commit/retrieval date. Pin reviewed versions. Remove unrelated connector dependencies. Do not execute installer commands found inside research material. No external skills are installed by this planning task.

## Agent specification

Name: **مدقق حساب — المحاسبة والمخزون / Hisab Accounting & Inventory Auditor**.

The specialist must:

1. Derive expected journal entries and financial effects from business transactions.
2. Reconcile stock quantities/values and customer/supplier subledgers to the ledger at identical cutoff dates.
3. Review Arabic accounting terminology, RTL screens, numeric input and printed documents.
4. Trace findings to requirements, code, tests, SQL results and screenshots.
5. Produce an Arabic business report and precise developer findings with English code identifiers where useful.

Use a separate reviewer context with the baseline commit, business rules, scope and synthetic fixtures. It derives expected results independently of the implementation's assertions. The developer fixes confirmed issues; the specialist independently retests them. A title or persona does not establish competence: the reviewer itself must pass calibration tests.

Proposed skill structure:

- `SKILL.md`: invocation, audit modes, evidence requirements and finding contract.
- `references/hisab-map.md`: maintained code/schema/report map and baseline.
- `references/accounting-controls.md`: posting, reversal, cutoff, reconciliation and closing controls.
- `references/inventory-controls.md`: costing, quantities, locations, serial ownership, reservations and custody.
- `references/arabic-jordan.md`: bilingual glossary, JOD precision, document fields and dated official-source register.
- `references/market-capabilities.md`: scoped benchmark of business workflows.
- `assets/scenarios/`: synthetic sequences and independently calculated expected results.
- `scripts/`: deterministic reconciliation/evidence helpers where repeatability justifies them.

Audit code and isolated fixtures. Do not post to live company data, alter production balances, upload business data or publish releases as part of an audit. Requested repairs use an isolated branch.

## Coverage matrix

| Area | Validation required |
|---|---|
| Setup and migration | Fiscal year, chart classification, opening trial balance, opening stock/customer/supplier detail and migration reconciliation |
| General accounting | Correct accounts, dates and debit/credit signs; atomic balanced posting; duplicate prevention; correction trace |
| Sales and receivables | Cash/credit/mixed payments, partial collections, advances, overpayments, discounts, returns after payment, credits and aging |
| Purchases and payables | Partial receipts/billing, unbilled goods, landed costs, returns, supplier credits, payments and reconciliation |
| Cash, banks and cheques | Bank reconciliation, cash movements, outstanding/deposited/returned cheques, fees and cutoff |
| Financial statements | Trial balance, classified balance sheet, income statement/gross profit, cash flows, comparatives, drill-down and retained earnings |
| Adjustments and assets | Accruals, prepayments, depreciation, asset register/disposals, bad debts and stock write-downs; distinguish possible manual journals from complete user workflows |
| Inventory | Quantity/value by item/location/date, moving average, transfers, counts, negative-stock rules, serial ownership, reservations and import idempotency |
| Inventory/accounting boundary | Inventory GL equals stock valuation at the same cutoff; COGS timing; backdated replay; closed-period protection; zero-quantity residual values |
| Warranties | Customer-owned faulty devices versus owned stock, replacement cost, inherited coverage, supplier recovery, reversal and subsequent claims |
| Tax and local documents | Applicable fields and classifications, taxable/exempt/zero-rated distinctions, recoverability, discounts/returns and rounding |
| Reliability and controls | Service-level permissions, audit trail, numbering, approvals/locks, backup/restore, interrupted writes, migration and representative data volumes |
| Arabic usability | مدين/دائن، ذمم مدينة/دائنة، سند قبض/سند صرف; Arabic/Western digits; decimal separators; Latin serials in RTL; print; equal Arabic/English financial results |

Verify applicable Jordanian requirements against dated primary sources. Do not assume one tax rate applies to everything or declare full compliance from a worksheet. Preserve the no-integration constraint; if an applicable obligation cannot be met within it, report the specific gap rather than conceal it or implement an integration. A qualified local accountant should validate material accounting-policy judgments and statutory interpretations. The AI agent does not issue professional certification.

## Independent tests and agent calibration

Start with at least 30 end-to-end scenarios across the matrix, including adverse paths, plus a complete simulated trading month through close, restore and subsequent-period reporting.

For each scenario record the opening state, documents/dates, expected journal lines, quantities/values/serial transitions, report totals, actual results, discrepancies, artifacts and reproducible commands. Calculate expectations independently using decimal arithmetic, not the posting functions under review.

Example, synthetic and without tax: open 10 units at JOD 100, receive 10 at JOD 140, sell 5 at JOD 200. Expected average cost: JOD 120; COGS: JOD 600; ending stock: 15 units/JOD 1,800; sales: JOD 1,000; gross profit: JOD 400. Derive backdated and reversal variants separately. Tax examples must state test assumptions and distinguish them from verified business applicability.

Test the specialist on disposable fixtures with both correct cases and seeded defects: balanced entries posted to the wrong account, wrong cutoff, duplicated serials, wrong cost, missing tax reversal, unauthorized posting and stale historical reports. Require detection of every seeded critical defect with evidence and no false release blocks on clean control cases. Report measured precision/recall and unknowns rather than assuming reliability from fluent Arabic.

Require exact reconciliation at supported JOD precision with documented rounding rules. Do not hide unexplained differences behind arbitrary tolerances. Include ambiguous-policy cases: the agent must identify uncertainty instead of inventing rules. Have a qualified Arabic-speaking accountant review the judgment/terminology answer set.

## Execution plan

| Phase | Work | Exit deliverable |
|---|---|---|
| A: Baseline | Freeze v0.11.0, map services/screens, reconcile docs, classify core/conditional/optional requirements | Capability matrix separating implementation status from independently verified status |
| B: Specialist | Write skill, source register, glossary and report template; validate skill structure | Versioned reviewer package with provenance |
| C: Calibration | Build independent fixtures and defect challenges | Measured detection results, clean controls and accountant-review queue |
| D: Baseline audit | Execute business-month scenarios, source tracing, bilingual UI and recovery checks | Arabic report, technical findings, evidence bundle and ranked roadmap |
| E: Repairs | Implement small batches in dependency order; reviewer retests | Verified fixes, regression evidence and updated matrix |
| F: Release | Reconcile final data, run required tests, verify upgrades/documents | Pass, conditional pass or fail with explicit unresolved blockers |

Finding template: ID, Arabic title, business impact, severity, capability, baseline commit, scenario, expected/actual, source/screen evidence, reproduction, remedy, regression test and status. Distinguish confirmed defect, absent capability, usability issue, policy question and unverified observation.

Priority: P0 data loss/corruption or major unauthorized posting; P1 materially wrong financial results or broken core workflows; P2 missing ordinary workflows/reports or serious usability gaps; P3 polish. Missing optional enterprise features are not automatically critical bugs. No unresolved P0/P1 issues in scope for a release-ready assessment; disclose remaining capability limits.

Likely development order, subject to audit: posting/reconciliation defects first; complete financial statements and historical valuation; bank reconciliation; opening-balance/subledger completeness; adjustments/fixed assets; purchasing and warranty supplier recovery; additional analytics and polish. Core completeness takes priority over stock-aging refinements.

## Primary sources reviewed

Research date: 2026-11-18. Exact source revision pinning is part of Phase B.

- [Anthropic finance plugin](https://github.com/anthropics/knowledge-work-plugins/blob/main/finance/README.md)
- [Journal-entry preparation](https://raw.githubusercontent.com/anthropics/knowledge-work-plugins/main/finance/skills/journal-entry-prep/SKILL.md)
- [Reconciliation](https://raw.githubusercontent.com/anthropics/knowledge-work-plugins/main/finance/skills/reconciliation/SKILL.md)
- [Financial statements](https://raw.githubusercontent.com/anthropics/knowledge-work-plugins/main/finance/skills/financial-statements/SKILL.md)
- [Close management](https://raw.githubusercontent.com/anthropics/knowledge-work-plugins/main/finance/skills/close-management/SKILL.md)
- [Superpowers independent review](https://raw.githubusercontent.com/obra/superpowers/main/skills/requesting-code-review/SKILL.md)
- [Superpowers verification](https://github.com/obra/superpowers/blob/main/skills/verification-before-completion/SKILL.md)
- [Arabic business-analysis skill](https://raw.githubusercontent.com/AbdoBasyioni/business-analysis-skill/main/SKILL.md)
- [ERPNext accounting reports](https://docs.frappe.io/erpnext/accounting-reports)
- [ERPNext accounting entries](https://docs.frappe.io/erpnext/accounting-entries)
- [ERPNext inventory accounting](https://docs.frappe.io/erpnext/accounting-of-inventory-stock)
- [IFRS Foundation IAS 2](https://www.ifrs.org/issued-standards/list-of-standards/ias-2-inventories/): cost/write-down reference; assess applicability and policy.
- [Jordan ISTD sales-tax legislation](https://istd.gov.jo/AR/Pages/قوانين_ضريبة_المبيعات): authoritative starting point, not a completed compliance review.

This plan does not install skills, modify application behavior, declare Hisab complete or publish another release.
