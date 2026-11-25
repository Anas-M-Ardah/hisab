# Isolated accounting audit runner

Requires Windows and .NET 10. Run from the repository root:

```powershell
dotnet build tests/Hisab.Audit/Hisab.Audit.csproj -c Release
dotnet tests/Hisab.Audit/bin/Release/net10.0-windows/Hisab.Audit.dll
dotnet tests/Hisab.Audit/bin/Release/net10.0-windows/Hisab.Audit.dll --all
dotnet tests/Hisab.Audit/bin/Release/net10.0-windows/Hisab.Audit.dll --reports
```

The first run executes the targeted accounting integrity cases. `--all` executes the full application regression suite, including those cases. A failed assertion produces a nonzero exit code. Every run prints its distinct entrypoint and newly generated synthetic fixture directory; detailed results are saved there as `test-results.txt`.

`--reports` opens a temporary synthetic-data window, renders the reconciliation screen and print pages in Arabic and English at font sizes 14 and 24, verifies fixture totals, and closes it. PNGs and `ui-checks.txt` are saved in that run's temporary directory. Normal application startup and live data remain unused.

The runner references the application assembly but never invokes `AppEntry.Main`. Its assembly and entrypoint are explicitly named `Hisab.Audit` and `Hisab.Audit.Program`. It accepts no database path and creates fixtures in a new GUID directory under the OS temporary directory, rejecting the live Hisab directory before creating files. Never substitute `Hisab.dll` or a copied application executable for this runner. Do not copy existing `bin`/`obj` into audit workspaces.

All data is synthetic. The targeted fixtures cover historical customer/supplier allocation capacity, revocation-day boundaries, partial allocation, voucher reversal, return/cancellation intervals, pending and cleared incoming/outgoing cheque bounces across closing, generic reversal protection, closed-entry stability, duplicate events, invalid dates, permissions, forced transaction rollback, and ordinary cancellation.

The closing/stock cases additionally cover return-cost remainders, earlier-return cancellation, preservation of closed return costs, contiguous closing and reopening, equal-cost late receipt bills and reversal, fractional/zero remainder billing, price-variance guards and mixed-bill rollback, safe Arabic/Western thousands grouping, and visibility of opening balances outside invoice aging.

Opening and late-billing cases cover posting versus existing-journal detail modes, original due dates, payment allocation, atomic preview/import, historical source capacity, reversal/amendment guards, Excel template validation, schema 7→8 migration and restore. Price-adjustment cases cover signed inventory/COGS allocation, depleted stock, partial billing, closed-entry invariance, future sale cost, reversal, closing and reopening. `--reports` also renders opening forms/previews/documents and the late-billing form in both languages.

Expected amounts are fixed from the business fixtures (JOD 100.000 invoice/payment, JOD 50.000 return); tests do not derive expected balances from the implementation under test. Entry snapshots compare the actual pre-close entries, not only whether journals balance. The runner is a regression tool, not a claim of complete accounting or statutory certification.
