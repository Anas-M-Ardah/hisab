# Evidence guide

Baseline v0.11.0 / a2899dd28af26fc4b25530ab64127cf772b45c8f. Execution took place November 18–19, 2026; files checked November 22, and the eight-input parser probe was rerun successfully that day. Accounting and inventory runtime evidence remains from November 19.

Read AUDIT-AR.md for the consolidated Arabic report, including scope, calibration results, priorities and the normal-startup incident. Source production files remain unchanged. The report and plan are local/uncommitted.

- baseline-regression.txt: 390 passing built-in checks, zero failures.
- accounting/coordinator-verification.txt and inventory/coordinator-verification.txt: independently rerun isolated domain probes. Raw money uses integer thousandths (100000 = JOD 100.000).
- accounting/AccountingAuditProbe.cs and inventory/InventoryAuditProbe.cs: added harness source, not production changes. Reproduction instructions in each findings.md refer to the isolated work/audit-2026-11-18 directories. Build explicit StartupObject before running; do not launch copied default Hisab output. A zero probe exit code means evidence collection completed, not that all financial scenarios passed.
- arabic/parser-probe.ps1: extracts the exact parser from this repository, compiles it independently and writes parser-results.json; this report copy has its source path adjusted for its new location.
- calibration-input.json, arabic/predictions.json and calibration-results.json: raw blind cases, reviewer predictions and coordinator scoring. Four seeded defects detected; no clean case marked defective; one clean case qualified because of costing-policy uncertainty. Exact classification 7/8, not a claim of expert certification.
- skill-manifest.json: seven installed skills, pinned upstream provenance, compatibility adaptations and hashes.

All database fixtures are synthetic. The documented accidental normal startup touched the live database file; no before-snapshot exists to prove unchanged contents. No business transactions were entered through that launch and no automatic restore was attempted. Do not omit this limitation when describing the audit.

Pending: full 30-scenario independent suite, complete business-month simulation, physical printing, broader UI language equality, applicable Jordanian legal review and qualified accountant policy sign-off. Confirmed financial defects should be fixed before describing the product as a fully validated accounting system.
