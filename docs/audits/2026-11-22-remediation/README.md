# Milestones 0–1: accounting corrections

Date: 2026-11-22. Base commit: `a2899dd28af26fc4b25530ab64127cf772b45c8f`; schema 7 remains unchanged. Changes are uncommitted. Scope: ACC-01 and ACC-02 from the [audit](../2026-11-19/AUDIT-AR.md), with the isolated runner required by the [implementation plan](../../plans/2026-11-22-accounting-inventory-remediation-plan.md).

## Implemented behavior

- **ACC-01:** allocations must fit both the voucher and invoice at all affected date boundaries, including revoked allocations, reversed vouchers, and later-cancelled returns. Effective intervals are start-inclusive and end-exclusive: funds released on a revocation date can be reallocated that day. Current and historical capacity are both checked.
- **ACC-02:** pending or cleared incoming/outgoing cheques can bounce in an open period after their issue/clearance period closes. For a pending incoming cheque, reverse the collection account against the customer; for an outgoing cheque, reverse the payable-cheque account against the supplier. For a cleared cheque, also reverse the clearance entry on the bounce date. Original journal entries remain intact. The original document receives reversal metadata and allocations end on the event date, retaining earlier revocations.
- New reversing documents, allocation changes, cheque state and audit records commit together. Invalid, future, closed-period, pre-clearance, duplicate and unauthorized transitions reject. Cancellation retains the same accounting correction mechanism; no terminal-state reopening is introduced.
- Generic `Reverse` is unchanged. Only the private cheque helper permits a new open-period event against the closed original cheque. It does not trigger stock replay.
- `Hisab.Audit` has an explicit separate entrypoint, rejects supplied database paths, and generates synthetic fixture directories. It never calls normal app startup. Tests are also included in the existing self-test suite.

## Evidence

- `milestone-one-red.txt`: before production edits, eight independent cases failed: AR/AP historical voucher reuse, AR/AP historical invoice capacity, and four pending/cleared incoming/outgoing cheque cases. These were assertions, not merely diagnostic printouts; runner exit code was 1.
- `milestone-one-full.txt`: full suite passed 447 assertions, zero failures, exit code 0. This includes the first 57 new integrity assertions and all 390 existing assertions. Subsequently four additional assertions were added to explicitly exercise the generic managed reversal's original-period guard; production code did not change after the full run.
- `milestone-one-targeted.txt`: final targeted run passed 61 assertions, zero failures, exit code 0, including those four additional guard assertions. Final build passed with zero warnings/errors. `source-hashes.json` identifies the exact reviewed source/test files.
- Build command: `../dotnet/dotnet.exe build tests/Hisab.Audit/Hisab.Audit.csproj -c Release --no-restore --nologo -v quiet`. Initial clean runner build omitted `--no-restore`. The new project has its own bin/obj; no old binaries were copied into it.
- Run commands: `../dotnet/dotnet.exe tests/Hisab.Audit/bin/Release/net10.0-windows/Hisab.Audit.dll` and the same with `--all`. Each output identifies its synthetic directory and entrypoint.
- Independent read-only reviewer found no blocking production defects. The sole minor test finding was that default generic reversal rejected at the cheque-screen guard before reaching the closed-period guard. Added a `managed:true` rejection assertion for all four cheque variants. Reviewer did not run tests; execution evidence is the coordinator's.

## Remaining limits

These fixes prevent new invalid allocations; they do not automatically repair previously saved historical allocation errors. No production data was opened for this batch, no schema migration was required, and no commit, push or release was performed. Existing audit findings for rounding, opening balances, closing scope, late supplier billing, reports and Arabic input remain open. The 30-scenario full-system acceptance plan and complete business-month simulation are not complete.

Pre-existing return-after-allocation reconciliation and generic public `managed` access were considered by the reviewer and left outside this focused patch; the general reversal guard was not weakened. Full report/UI usability and professional accounting policy sign-off are not claimed by these service-level tests.
