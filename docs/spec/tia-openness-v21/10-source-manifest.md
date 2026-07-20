# TIA Portal V21 source manifest

This document records the XML inputs used to create the specification set.

| File | Assembly | Bytes | Members | SHA-256 |
|---|---|---:|---:|---|
| `Siemens.Engineering.AddIn.Base.xml` | `Siemens.Engineering.AddIn.Base` | 237429 | 553 | `51f266464e002b487150e1e29b0a9459d208f615c530445f8da08952830f9d2a` |
| `Siemens.Engineering.AddIn.Permissions.xml` | `Siemens.Engineering.AddIn.Permissions` | 4199 | 14 | `909c72a74bebc640ef6866c5023e241a54711dab0a7d365936f0ea994c94feb3` |
| `Siemens.Engineering.AddIn.Safety.xml` | `Siemens.Engineering.AddIn.Safety` | 20639 | 47 | `39271b7793fa19f906bf4084c26fa6e796e801049485e47cd48729ed0cd5129a` |
| `Siemens.Engineering.AddIn.Step7.xml` | `Siemens.Engineering.AddIn.Step7` | 26667 | 67 | `852a8a5585864f77ff278940ec018c49f1b2dd8e0529ab142b75e90a2d1191fb` |
| `Siemens.Engineering.AddIn.Utilities.xml` | `Siemens.Engineering.AddIn.Utilities` | 11828 | 61 | `d6c14998a822ffda5c8077a992d8be07639cb1a3a6297b1c10f4d06e66934ac3` |
| `Siemens.Engineering.Base.xml` | `Siemens.Engineering.Base` | 4622874 | 16983 | `0bf27f4ab7918c811a7dfde7f7c294b3133169b5f791e507278bfd27182cea43` |
| `Siemens.Engineering.Safety.xml` | `Siemens.Engineering.Safety` | 127638 | 283 | `b969c90b05c413b9f03b90888b1adcd2b21997005ef4b92db53b85a6b186d98d` |
| `Siemens.Engineering.SafetyValidation.xml` | `Siemens.Engineering.SafetyValidation` | 145637 | 316 | `c435bbf13890630290d8623b6a398437158d7d7dedc5c95867188f7f9f21da96` |
| `Siemens.Engineering.Step7.xml` | `Siemens.Engineering.Step7` | 1385941 | 3207 | `344f0652a1b10fedca6cd3501434ae8b1a8c4c0a014b3ef0ff1ef444f3fddda0` |
| `Siemens.Engineering.TeamcenterGateway.xml` | `Siemens.Engineering.TeamcenterGateway` | 144242 | 280 | `206ed2ea306c210341c71e856de224e20c3c9c82ebc5dfc7f4fb28895bfc501d` |
| `Siemens.Engineering.WinCC.Extension.xml` | `Siemens.Engineering.WinCC.Extension` | 7864 | 31 | `1b7eb100a6900509ceb548bb35c72e81087cf5ab0af1aa850bf04db991f45307` |
| `Siemens.Engineering.WinCC.xml` | `Siemens.Engineering.WinCC` | 600387 | 2008 | `2d1c7e1816b43ab10ead3036d37e007d15540ddbddc6a1a239635f71e43e7615` |
| `Siemens.Engineering.WinCCUnified.xml` | `Siemens.Engineering.WinCCUnified` | 2468634 | 6629 | `894afc507742b3498a3be4273e6818b05677103ecdd4687af1c6d1caa4a5f73b` |

## Extraction notes

- XML member names preserve the .NET documentation identifier prefixes: `T:` type, `M:` method, `P:` property, `F:` field and `E:` event.
- Some XML summaries contain placeholders or sparse text. The narrative specifications avoid inventing behavior when the XML does not describe it.
- Runtime availability can depend on installed TIA products, project configuration, device family, licensing, access level and trust.
- The generated catalog is an index. The installed V21 assemblies remain the executable source of truth.
