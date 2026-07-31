# Fix VNPay Signature Bug Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the `vnp_IpnUrl` parameter from the VNPay payment URL generation to fix the signature mismatch bug (Error Code 70).

**Architecture:** The `CreatePaymentUrl` method in `VNPayService.cs` incorrectly includes `vnp_IpnUrl` in the `vnpParams` dictionary when generating the payment redirect URL and computing the `vnp_SecureHash`. The VNPay standard for v2.1.0 does not accept `vnp_IpnUrl` as a query parameter in the `pay` command, leading to a hash mismatch on the VNPay server. By removing this block, the backend generates a valid standard request.

**Tech Stack:** C#, .NET 8/10, VNPay v2.1.0

## Global Constraints

- Must follow C# coding standards and keep the code compiling successfully.
- Do not remove the `ipnUrl` parsing from `appsettings.json` validation, just remove its usage from `CreatePaymentUrl` parameters.

---

### Task 1: Remove vnp_IpnUrl from CreatePaymentUrl

**Files:**
- Modify: `Aivora.Services/WalletService/VNPayService.cs`

**Interfaces:**
- Consumes: `VNPayService.cs` existing methods
- Produces: Updated `CreatePaymentUrl` that generates valid VNPay URLs.

- [ ] **Step 1: Write the minimal implementation**

```csharp
// Remove the following variable declaration around line 59:
// var ipnUrl = _configuration["VNPay:IpnUrl"];

// Remove the following block around lines 81-85:
// // Only include IpnUrl if configured; VNPAY sandbox may reject custom IpnUrl
// if (!string.IsNullOrEmpty(ipnUrl))
// {
//     vnpParams["vnp_IpnUrl"] = ipnUrl;
// }
```

- [ ] **Step 2: Run build to verify compilation**

Run: `dotnet build`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Aivora.Services/WalletService/VNPayService.cs
git commit -m "fix: remove vnp_IpnUrl from VNPay payment url to fix signature mismatch"
```
