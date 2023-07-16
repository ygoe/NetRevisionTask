# .NET Revision Task for MSBuild

Injects the current version control system (VCS) revision of a working directory in a custom format into a .NET assembly build.

[![NuGet](https://img.shields.io/nuget/v/Unclassified.NetRevisionTask.svg)](https://www.nuget.org/packages/Unclassified.NetRevisionTask)

## Introduction

Based on the idea of the .NET Revision Tool, which is a standalone executable that could print out revision information and patch and restore source files before and after a build, this is a **custom MSBuild task** that tightly integrates with the standard build process. It now supports .NET Core projects in VS 2017 (.csproj format) by providing the version information through **MSBuild properties like `Version` and `InformationalVersion`** instead of writing to source files directly. Patching and restoring source files is still supported as a fallback mechanism for classic-style projects (VS 2015 and WPF/.NET Framework).

Currently the following VCS are supported:

* Git
* Subversion

More systems can easily be added in the code.

## Why?

Every bigger-than-small application has a version number that the user can query in some form of About dialog window. You should use it to make newer versions of your software distinguishable from older ones. Keeping it up-to-date in the source code is often more work than desired and leads to errors resulting in inconsistent version descriptions.

By automating the copying of that revision ID into the application source code, you can avoid forgetting that update. Also, possible keyword replacing features of Git/SVN itself do not play very well with C#/VB.NET source code or project files in creating a friendly version for that assembly. .NET Revision Task is optimised for this scenario and can adapt to special wishes.

If you release often and don’t want to manage [semantic version numbers](https://semver.org/) in the form major.minor.patch (as for .NET Revision Task itself), you might just use the Git or SVN revision identifier or commit time as version number for your program. But semantic versions are also supported.

## Installation

Just install the NuGet package **Unclassified.NetRevisionTask** to your **.NET Framework 4.6** or **.NET Standard 1.6** project in VS 2015 or later and it starts working for you.

This tool has so far only been tested when building on Windows. It *may* also work when building .NET Core projects on other platforms. If you have any issues with that, please open an issue.

If you’re **creating a NuGet package** of your project, make sure to declare this package reference as private in your .csproj so that your final package does not depend on NetRevisionTask, which it really doesn’t.

    <ItemGroup>
      <PackageReference Include="Unclassified.NetRevisionTask" Version="..." PrivateAssets="all" />
    </ItemGroup>

### Default behaviour for Git

If not configured otherwise, tags following semantic versioning ([SemVer 2.0.0](https://semver.org/)) will be considered to determine the assembly version. The expected tag name format is “v1.2.3”, with a leading lower-case “v”. (Abbreviated names like “v1” or “v1.2” should also work.) Revisions that are not directly tagged are considered a pre-release after the last version tag and the branch name and number of commits after the tag will be appended, together with the abbreviated commit hash as build info. This is a production-ready default that you may want to keep. Its format is defined as:

    {semvertag+chash}{!:-mod}

Examples:

* 0.0.1-master.1+abcdef1
* 1.0.1-feature-20.3+abcdef1
* 1.2.0

If you want to keep this but not include the branch name for the main branch, e.g. “master”, you could change it to this:

    {semvertag:master:+chash}{!:-mod}

### Default behaviour for Subversion

If not configured otherwise, the revision number is used as the patch number. You should change that before you make a release. Its format is defined as:

    0.0.{revnum}{!:-mod}

 Examples:

* 0.0.1
* 0.0.20
* 0.0.358

## Configuration

### MSBuild properties

Configuration of the version scheme is done through MSBuild properties defined in the project file to which the NuGet package was added. If you have multiple projects in your solution, you’d basically have to repeat these steps for each project, or you could factor them out into a separate .props file and import that into each project or you can use [Directory.Build.props](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#directorybuildprops-and-directorybuildtargets). This also applies to classic-style projects, even though editing the project file is a bit more involved.

Example:

    <PropertyGroup>
      <NrtRevisionFormat>{semvertag}</NrtRevisionFormat>
      <NrtResolveSimpleAttributes>true</NrtResolveSimpleAttributes>
      <NrtResolveInformationalAttribute>true</NrtResolveInformationalAttribute>
      <NrtResolveCopyright>true</NrtResolveCopyright>
      <NrtTagMatch>v[0-9]*</NrtTagMatch>
      <NrtRemoveTagV>true</NrtRemoveTagV>
      <NrtRequiredVcs>git</NrtRequiredVcs>
      <NrtShowRevision>true</NrtShowRevision>
      <NrtProjectDirectory>$(MSBuildProjectDirectory)</NrtProjectDirectory>
      <NrtResolveMetadata>true</NrtResolveMetadata>
      <NrtErrorOnModifiedRepoPattern>.*Release.*</NrtErrorOnModifiedRepoPattern>
    </PropertyGroup>

The following MSBuild properties are supported:

**NrtRevisionFormat**: string, default: automatic.

The revision format template. This is automatically detected from the AssemblyInfo file in your project, if it exists. It can be overridden for .NET Core/Standard projects.

**NrtResolveSimpleAttributes**: boolean, default: true.

Specifies whether simple version attributes are resolved to the determined version. This affects `Version` (`AssemblyVersionAttribute`) and `FileVersion` (`AssemblyFileVersionAttribute`). The `Version` identifies the assembly and must be dotted-numeric (max. 65535). The `FileVersion` is saved in the Win32 file version resource and must be dotted-numeric (max. 65535). Anything that doesn’t fit into this scheme is truncated from the determined version before setting it to these attributes.

**NrtResolveInformationalAttribute**: boolean, default: true.

Specifies whether the informational version attribute is resolved to the determined version. This affects `InformationalVersion` (`AssemblyInformationalVersionAttribute`). It is a free descriptive text that may contain version names like “beta” or a VCS commit hash.

**NrtResolveCopyright**: boolean, default: true.

Specifies whether the copyright year is resolved to the current year. This affects `Copyright` (`AssemblyCopyrightAttribute`). It usually just contains the `{copyright}` placeholder or a variant of it.

**NrtTagMatch**: glob pattern, default: v[0-9]*

The pattern of tag names to match when looking for version tags. These tag names usually look like “v0.1” or “v2.1.5”. Other tags should be ignored for this use. This should usually not be changed.

**NrtRemoveTagV**: boolean, default: true.

Specifies whether the “v” prefix of a matching version tag should be removed to determine its version. This should usually not be disabled.

**NrtRequiredVcs**: string, default: “”.

Specifies the name of the VCS that is expected to be found in the project directory. Can be “git” or “svn” (`IVcsProvider.Name`).

**NrtShowRevision**: boolean, default: false.

Specifies whether the determined revision ID is printed during the build with higher importance than normal, so it can be seen more easily. When patching the AssemblyInfo file, it is also displayed to the console.

**NrtProjectDirectory**: string, default: $(MSBuildProjectDirectory).

Sets the directory where NRT starts searching for the VCS files. This is helpful if NRT is added to a project that is a submodule of another repository and should observe the parent repository.

**NrtResolveMetadata**: boolean, default: true.

Specifies whether the value component of the `AssemblyMetadata` (`AssemblyMetadataAttribute`) is resolved.

**NrtErrorOnModifiedRepoPattern**: string, default: “”.

Specifies a case-insensitive RegEx pattern string matching the build configuration string to trigger a build error if the repository contains modifications. If the string is empty, the functionality is disabled.

### Revision format

You can customise the format of the resulting version with a revision format string that defines how information about the commit or revision is formatted into the final revision ID. It is a plain string that contains placeholders in `{curly braces}`. Each placeholder is a simple data field or encodes a time value using a scheme and optional configuration arguments.

The following data field placeholders are supported:

**`{chash}`**: Full commit hash.

**`{CHASH}`**: Full commit hash, in upper case.

**`{chash:<length>}`**: Commit hash truncated to the specified length. (Also for upper case)

**`{revnum}`**: Revision number.

**`{revnum-<offset>}`**: Revision number minus the offset. (Also available with +)

**`{!}`**: The “!” character if the working directory is modified, otherwise empty.

**`{!:<string>}`**: The specified string if the working directory is modified, otherwise empty.

**`{cname}`, `{cmail}`**: Committer’s name or e-mail address.

**`{aname}`, `{amail}`**: Author’s name or e-mail address.

**`{mname}`**: Build machine name (computer name).

**`{branch}`**: Currently checked-out branch.

**`{branch:<sep>:<ref>}`**: Branch name, if not `<ref>` or empty, separated by `<sep>`, otherwise empty.

**`{semvertag}`**: Semantic version based on the most recent matching tag name. Revisions that are not directly tagged are considered a pre-release after the last tag (the patch value is incremented by 1) and the branch name and number of commits after the tag will be appended.

**`{semvertag+chash}`**: Semantic version based on the most recent matching tag name, see `{semvertag}`. Pre-releases also have the abbreviated commit hash appended after a plus (`+`) sign as build info. This is part of the default format for Git repositories.

**`{semvertag+chash:<length>}`**: Same as `{semvertag+chash}` but with the commit hash truncated to the specified length instead of the default 7.

**`{semvertag+CHASH:<length>}`**: Same as `{semvertag+chash:<length>}` but with the commit hash in upper case.

**`{semvertag:<defaultbranch>}`**: Same as `{semvertag}` but with the branch name left out if it is equal to `<defaultbranch>`.

**`{semvertag:<defaultbranch>:+chash}`**: Same as `{semvertag+chash}` but with the branch name left out if it is equal to `<defaultbranch>`.

**`{semvertag:<defaultbranch>:+chash:<length>}`**: Same as `{semvertag+chash:<length>}` but with the branch name left out if it is equal to `<defaultbranch>`.

**`{semvertag:<defaultbranch>:+CHASH:<length>}`**: Same as `{semvertag+CHASH:<length>}` but with the branch name left out if it is equal to `<defaultbranch>`.

**`{tag}`**: Most recent matching tag name, with additional info.

**`{tagname}`**: Most recent matching tag name only.

**`{tagadd}`**: Number of commits since the most recent matching tag.

**`{tagadd:<sep>}`**: Number of commits since the most recent matching tag, prefixed with `<sep>`, or empty.

**`{tz}`**: Local time zone offset like “+02:00”.

**`{url}`**: Repository URL.

**`{copyright}`**: Abbreviation for the copyright year (commit or build time).

**`{copyright:<first>-}`**: Abbreviation for the copyright year range, starting at `<first>`. The following dash is optional but recommended for clearer understanding.

**`{bconf}`**: Build configuration.

**`{BCONF}`**: Build configuration, in upper case.

**`{bconf:<sep>:<ref>}`, `{BCONF:<sep>:<ref>}`**: Build configuration, if not matching case-insensitive RegEx `<ref>` pattern, separated by `<sep>`, otherwise empty.

Schemes convert a commit or build time to a compact string representation. They can be used to assign incrementing versions if no revision number is provided by the VCS. First, select from the build, commit or authoring time with `{b:…}`, `{c:…}` or `{a:…}`. This is followed by the scheme name. There are 4 types of schemes.

The following time schemes are supported:

**Readable date/time:** Produces a readable date or time string in several formats.

| Format | Description |
|---|---|
| `ymd` | Year, month, day, no separator. |
| `ymd-` | Year, month, day, separated by “-”. |
| `ymd.` | Year, month, day, separated by “.”. |
| `hms` | Hour, minute, second, no separator. |
| `hms-` | Hour, minute, second, separated by “-”. |
| `hms:` | Hour, minute, second, separated by “:”. |
| `hms.` | Hour, minute, second, separated by “.”. |
| `hm` | Hour, minute, no separator. |
| `hm-` | Hour, minute, separated by “-”. |
| `hm:` | Hour, minute, separated by “:”. |
| `hm.` | Hour, minute, separated by “.”. |
`h` | Hour only. |

Prefix with “u” for UTC instead of local time zone.

**Dotted-decimal:** Generates regular dotted version numbers with two segments. The first describes the days since the base year, the second the number of intervals since midnight (UTC). This scheme consists of multiple colon-separated values: interval length, base year.

The interval length is a number followed by “s” for seconds, “m” for minutes, “h” for hours, or “d” for days. Practical intervals are “15m” (2 digits), “2m” (3 digits).

A shortcut to the 15-minute interval is `{dmin:<year>}`.

**Base-encoding:** Converts a linear value to a higher number base to create more compact digit/letter combinations. These schemes consist of multiple colon-separated values: number base, interval length, base year, minimum output length.

Number base can be from 2 to 36. The digits 0–9 and then letters a–z are used. The higher the base, the higher the chance that profane words appear in a revision ID. **Base 28** uses an optimised alphabet without vowels and similar characters to avoid errors when hand-writing and undesired words.

The number of passed intervals since the base year is encoded for the result (UTC). The minimum length padding generates fixed-length comparable strings. Set the length to a value that lasts for as long as you plan to use this versioning scheme (30 years recommended). Practical combinations are “16:1m” (6 chars), “28:20m” (4 chars), “36:10m” (4 chars).

All letters are lower case. Use a capital `{A:…}`, `{B:…}` or `{C:…}` for upper case.

**Hours:** Generates a single number of hours passed since the given base year and month. This scheme begins with “h:” followed by two hyphen-separated values: base year, base month.

For values up to 65535 this lasts over 7 years.

**Examples:**

| Format | Description |
|---|---|
| `{b:ymd-}`      | Local build date, like “2015-12-31”. |
| `{c:hm.}`       | Local commit time of day, like “23.59”. |
| `{c:uhm}`       | UTC commit time of day, like “2359”. |
| `{c:15m:2015}` | Dotted decimal from commit time since 2015, like “365.95”. |
| `{dmin:2015}`  | Same as previous (short syntax from version 1.x). |
| `{c:16:1m:2014}` | Base-16 encoding of minutes from commit time since 2014, like “abcdef”. |
| `{b:28:20m:2013:4}` | Base-28 encoding of 20-minute intervals from build time since 2013, like “1xy9”. |
| `{bmin:2013:4}` | Same as previous (short syntax from version 1.x). |
| `{B:28:20m:2013:4}` | Base-28 encoding (upper case) of 20-minute intervals from build time since 2013, like “1XY9”. |
| `{b:h:2015-02}` | Hours encoding of UTC build time since February 2015, like “912”. |

## Usage in C# source code

The following sample code from the AssemblyInfo.cs file would be resolved as described. Defining the desired attributes and their revision formats in this file is the easier and recommended method for classic-style projects (i.e. what existed before .NET Core).

    [assembly: AssemblyVersion("0.0")]
    [assembly: AssemblyInformationalVersion("1.{c:15m:2013}-{chash:6}-{c:ymd}")]

Result:

    [assembly: AssemblyVersion("1.93.42")]
    [assembly: AssemblyInformationalVersion("1.93.42-45d4e3-20130401")]

Only **C#** and **VB.NET** projects and source files can be patched. Other source code language files will not be found. This restriction should not apply to new-style simplified projects (like .NET Core).

## Other usages

Although this is an MSBuild extension, you can use its core functionality in any environment. As long as you stay away from the `NetRevisionTask.Tasks` namespace, you should be fine calling any public API even without MSBuild DLLs available.

PowerShell example:

    Add-Type -Path NetRevisionTask.dll
    [NetRevisionTask.Api]::GetVersion()

Batch example:

    @echo off
    %SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -Command "Add-Type -Path NetRevisionTask.dll; [NetRevisionTask.Api]::GetVersion()"
    exit /b %errorlevel%

## VCS providers

Git (for Windows) CLI must be installed using the setup for Windows or in one of %ProgramFiles*%\\Git* or in the PATH environment variable. The revision number is provided by counting revisions with the `--first-parent` option in the current branch. This is a stable value for the master branch if merges from other branches are done the correct way (always merge temporary/work/feature branches into master, not reverse).

SVN CLI (svn and svnversion) must be available on the system. This is included in the “CLI” option of the TortoiseSVN setup. Other locations are considered (see source code).

## License

[MIT license](https://github.com/ygoe/NetRevisionTask/blob/master/LICENSE)
