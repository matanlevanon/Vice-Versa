# Code signing

**Current decision: ship unsigned.** The workflow warns and keeps going. Users tick Unblock in file properties, or click through SmartScreen. This document is here for when that stops being acceptable.

Windows shows a SmartScreen warning on unsigned downloads, and Defender treats them with suspicion. Removing the warning requires a certificate issued to a verified identity.

This document lists the options, what each costs, and exactly what to configure. The build workflow already supports two signing paths. Add secrets and the next build comes out signed. No code changes.

Prices and eligibility below were checked in August 2026. Verify before buying, because Microsoft has been moving all of this around.

> **The one thing to know before reading further.** Only one route removes the warning on the first download: publishing through the Microsoft Store, which Microsoft signs itself. Every purchasable certificate, at any price, builds SmartScreen reputation gradually from download volume. Since 2024 this includes EV certificates, which used to grant instant reputation and no longer do.

---

## What signing buys you

Two separate things:

1. **A valid signature.** Proves the file has not been altered since you signed it and names the publisher. Any certificate does this, self-signed included.
2. **SmartScreen reputation.** Decides whether Windows shows the blue "Windows protected your PC" screen. Reputation attaches to the certificate identity, not the file.

A self-signed certificate gives you the first and none of the second.

---

## Option 1: Publish through the Microsoft Store

**Free.** The $19 registration fee was waived in May 2026 for both individual and company accounts, and the new onboarding covers roughly 200 markets, Israel included. Packaging tools are free. Microsoft takes a revenue share only on paid apps.

**This is the only route with no SmartScreen warning on day one.** You do not hold a certificate at all: you submit an MSIX package, Microsoft certifies it and re-signs it with their own certificate, which every Windows machine already trusts.

What it costs that is not money:

- Identity verification with a government ID and a selfie, captured on mobile. Minutes.
- Multi-factor authentication on the account.
- Packaging the app as MSIX rather than a plain executable.
- Certification review on every submission, usually hours, sometimes a couple of days. Releases stop being "push a tag and walk away".

**Two catches specific to this app.** MSIX runs the app in a light container with registry virtualisation, so the `HKCU\Run` write behind "Start with Windows" is silently virtualised away and must move to the MSIX `StartupTask` extension. Portable mode is meaningless in a Store build. The global hotkey and `SendInput` both work normally in a full-trust packaged desktop app.

**The coverage catch.** A Store signature covers the copy installed from the Store. The portable executable and installer on the GitHub Releases page stay unsigned and keep warning. Store gets you one clean channel, not a clean binary everywhere.

---

## Option 2: Azure Artifact Signing

Formerly called Trusted Signing. Around **$9.99 per month** for the Basic tier, which covers 5,000 signatures a month. No hardware token, no HSM, no shipping. Integrates cleanly with GitHub Actions, which is why the workflow already has a path for it.

**Reputation still builds over time.** Microsoft's own guidance puts Artifact Signing in the reputation-building bucket alongside OV and EV certificates, so early downloads still warn. An earlier version of this document claimed reputation was immediate. That was wrong.

**Eligibility now includes Israel, for organisations.** Microsoft lists public trust certificates as available to organisations in the US, Canada, EU, UK, Australia, New Zealand, Japan, South Korea, Singapore, Switzerland, Norway and Israel. **Individual developers are still limited to the US and Canada.** So this needs a legal entity whose name and address match the Azure billing account exactly. Identity validation takes 1 to 20 business days.

### Setting it up

1. Create an Azure subscription.
2. Create an Artifact Signing account and a certificate profile. Complete identity verification, which takes a few business days.
3. Register an Entra ID app registration, generate a client secret, and grant it the **Trusted Signing Certificate Profile Signer** role on the signing account.
4. Add these repository secrets under Settings, Secrets and variables, Actions:

| Secret | Value |
|---|---|
| `AZURE_TENANT_ID` | Entra tenant ID. Presence of this secret is what selects the Azure signing path. |
| `AZURE_CLIENT_ID` | App registration client ID |
| `AZURE_CLIENT_SECRET` | App registration client secret |
| `AZURE_SIGNING_ENDPOINT` | Region endpoint, for example `https://weu.codesigning.azure.net` |
| `AZURE_SIGNING_ACCOUNT` | Signing account name |
| `AZURE_CERT_PROFILE` | Certificate profile name |

The workflow step uses `azure/trusted-signing-action`. Microsoft has been renaming this service, so if the action fails to resolve, check whether it has moved to `azure/artifact-signing-action` and update the two step definitions in `.github/workflows/build.yml`.

---

## Option 3: Buy an OV certificate from a CA

Works from anywhere, Israel included, individual or company. Reputation builds over time from downloads, so early users still see the warning for a while.

Since June 2023 every code signing private key must live on hardware or in a CA-hosted HSM. Cloud-based products avoid shipping a physical token and work with CI.

| Product | Rough price | Notes |
|---|---|---|
| Certum Open Source Code Signing (cloud) | around **$60 to $110 per year** | Cheapest real certificate. Requires the project to be genuinely open source, which this one is. Signing runs through Certum's SimplySign cloud, so CI integration needs their tooling rather than a plain PFX. |
| Certum Cloud Code Signing, individual developer | around **$120 to $180 per year** | Same cloud model, no open source requirement. |
| SSL.com, SignMyCode, SSLmentor OV resellers | **$200 to $400 per year** | Cloud HSM options available. Shop resellers, not the CA's list price. |
| EV certificates | **$250 to $500 per year** | Gave instant SmartScreen reputation until Microsoft removed that behaviour in 2024. EV-signed files now build reputation exactly like OV-signed ones. There is no longer a reason to pay the premium. |

### If your CA gives you a PFX

Some cloud products let you export a PFX, or you generate one locally for testing. Add:

| Secret | Value |
|---|---|
| `SIGNING_PFX_BASE64` | Base64 of the `.pfx` file. Presence of this secret selects the PFX signing path. |
| `SIGNING_PFX_PASSWORD` | Password protecting the `.pfx` |

Produce the base64 value with:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('cert.pfx')) | Set-Clipboard
```

Optionally set a repository variable `TIMESTAMP_URL` to override the default timestamp server.

Timestamping matters. A timestamped signature stays valid after the certificate expires. The workflow always timestamps.

### If your CA gives you cloud HSM access only

Certum SimplySign, SSL.com eSigner and similar need their own signing client instead of `signtool /f`. Replace the PFX step in the workflow with the CA's action or CLI. The rest of the pipeline stays as it is.

---

## Option 4: SignPath Foundation, free for open source

[SignPath Foundation](https://signpath.org/) issues free certificates to qualifying open source projects. This is the only genuinely free route to a trusted certificate.

Their conditions, roughly:

- OSI-approved license with no commercial dual-licensing
- No proprietary code in the binary
- Actively maintained, already released, documented
- Signed binaries must come from an automated build off the repository, which this project already does
- Multi-factor authentication on the team, defined signing roles, and a published code signing policy
- They verify project reputation before accepting, and acceptance is at their discretion

A brand new repository with no users is unlikely to be accepted. Worth applying once the project has real traction.

---

## Option 5: Self-signed, free, for your own machines

A self-signed certificate produces a valid signature. Windows does not trust the root, so SmartScreen still warns unless the certificate is installed into the machine's Trusted Root store. That is fine on machines you control and worthless on anyone else's.

```powershell
./tools/New-SelfSignedCert.ps1 -Subject 'Your Name' -Password 'choose-one' -Trust
```

`-Trust` installs the certificate into Trusted Root and Trusted Publisher on the local machine. The script also writes a `.cer` file to import on other machines you control.

Feed the resulting `.pfx` into CI through `SIGNING_PFX_BASE64` if you want signed builds while you decide.

---

## Recommendation

| Situation | Take |
|---|---|
| **Not ready to decide. This is where the project is today.** | Ship unsigned. The workflow warns and keeps going. Users tick Unblock in file properties, or click through SmartScreen. |
| You want the warning gone, properly, and will accept MSIX packaging and a review queue | Microsoft Store, free, no warning from the first install |
| You want to keep shipping a plain exe from GitHub and reduce warnings over time | Azure Artifact Signing at $10 a month if you have an Israeli company, otherwise Certum Open Source at roughly $60 to $110 a year |
| Only your own machines matter for now | Self-signed with `-Trust`, free |
| Project has real users and you want free | Apply to SignPath Foundation |

The two-channel setup, Store for a warning-free install plus a paid certificate for the direct downloads, is the only way to cover everyone. It also costs both money and release friction, so hold it until people are downloading this in real numbers.

---

## Reducing warnings without a certificate

These help a little. None replace a certificate.

- Publish SHA256 checksums with every release. The workflow already does.
- Link users to the GitHub Actions run that produced the binary, so the build is auditable.
- Tell users to right-click the download, open Properties, and tick **Unblock**.
- Submit false positives to [Microsoft's malware analysis portal](https://www.microsoft.com/en-us/wdsi/filesubmission).

---

## Sources

- [Azure Artifact Signing pricing](https://azure.microsoft.com/en-us/pricing/details/trusted-signing/)
- [Azure Artifact Signing product page](https://azure.microsoft.com/en-us/products/artifact-signing)
- [Azure Artifact Signing quickstart, including the supported country list](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart)
- [SmartScreen reputation for Windows app developers](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
- [Free developer registration for individual developers, Microsoft Store](https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-individual-developer)
- [Publish to Microsoft Store as a company, free registration, May 2026](https://blogs.windows.com/windowsdeveloper/2026/05/07/publish-to-microsoft-store-as-a-company-now-with-free-registration-and-faster-onboarding/)
- [Code signing on Windows with Azure Artifact Signing, Melatonin](https://melatonin.dev/blog/code-signing-on-windows-with-azure-trusted-signing/)
- [Certum Open Source Code Signing on SimplySign](https://certum.store/open-source-code-signing-on-simplysign.html)
- [SignPath Foundation conditions](https://signpath.org/terms.html)
- [Code signing options for Windows app developers, Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)
