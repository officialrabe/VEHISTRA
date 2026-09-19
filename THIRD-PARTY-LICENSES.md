# Third-party components

Vehistra itself is licensed under the MIT License (see [`LICENSE`](LICENSE)).
This file lists every third-party component it ships or builds with, so that
anyone – in particular the reviewers of the SignPath Foundation – can check the
licence situation without reading the project files.

Versions are managed centrally in
[`Directory.Packages.props`](Directory.Packages.props); the versions below are
the ones current for release 1.0.0.

## Shipped with the application

| Component | Version | Licence |
| --- | --- | --- |
| Microsoft.EntityFrameworkCore (+ `.Relational`, `.SqlServer`, `.Sqlite`) | 10.0.12 | MIT |
| Microsoft.Data.SqlClient | 6.1.6 | MIT |
| SQLitePCLRaw (transitive, via EF Core SQLite) | – | Apache-2.0; the bundled SQLite engine is public domain |
| Microsoft.Extensions.* (Hosting, DependencyInjection, Configuration, Logging.Abstractions) | 10.0.12 | MIT |
| System.Security.Cryptography.ProtectedData | 10.0.12 | MIT |
| CommunityToolkit.Mvvm | 8.4.2 | MIT |
| Serilog (+ `.Extensions.Logging`, `.Sinks.File`, `.Sinks.Console`) | 4.4.0 / 10.0.0 / 7.0.0 / 6.1.1 | Apache-2.0 |
| ClosedXML | 0.105.1 | MIT |
| CsvHelper | 33.1.0 | MS-PL **or** Apache-2.0, at the user's choice |
| QuestPDF | 2026.9.0 | Community and Commercial dual licence – see the note below |

## Build and test only, not shipped

| Component | Version | Licence |
| --- | --- | --- |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | MIT |
| Microsoft.NET.Test.Sdk | 18.10.1 | MIT |
| xunit.v3 | 4.0.1 | Apache-2.0 |
| Shouldly | 4.3.0 | BSD-3-Clause |
| Inno Setup 6 (installer compiler, run in CI) | 6.x | Inno Setup licence (free, permits commercial use) |
| DejaVu Sans Mono ([`assets/fonts/`](assets/fonts/)) | – | Bitstream Vera licence, free; full text in [`assets/fonts/DejaVuSansMono-LICENSE.txt`](assets/fonts/DejaVuSansMono-LICENSE.txt) |

The font is used by the documentation builder and its glyphs are embedded in
the generated PDF guides, which are shipped; its licence permits that.

The runtime itself – .NET 10 Desktop Runtime – is installed by the user from
Microsoft and is not redistributed by Vehistra.

## Note on QuestPDF

QuestPDF is not published under a single OSI-approved licence. It uses a
Community/Commercial dual licence model: the Community licence is free for
individuals, non-profits, organisations below USD 1,000,000 annual gross revenue
and – explicitly – for **open-source projects under OSI-approved licences**.
Vehistra is such a project (MIT), so it uses QuestPDF under the Community
licence.

We state this openly rather than leave it to be discovered: anyone who takes
Vehistra's MIT-licensed source and ships it outside those conditions has to
check QuestPDF's terms for their own case. QuestPDF is used in
`Vehistra.Reporting` to generate the PDF reports.

## How this list is kept honest

Adding a package means adding a row here in the same pull request. The list is
not generated automatically, because a generator reports what a package claims
in its metadata, not what its licence actually says – as the QuestPDF entry
above shows.
