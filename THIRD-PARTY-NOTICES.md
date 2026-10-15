# Third-party notices

Jonsbo D34 Mon itself is MIT-licensed (see [LICENSE](LICENSE)). It uses or ships the components below,
each under its own license. Their source code is available at the links given.

| Component | Used for | License | Source |
|---|---|---|---|
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 0.9.6 | CPU temperature, load, clocks | MPL-2.0 | github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| LibreHardwareMonitorLib dependencies (HidSharp, DiskInfoToolkit, RAMSPDToolkit-NDD, System.Management, …) | pulled in by the library (not used for the CPU-only setup, but bundled) | Apache-2.0 / MIT / MPL-2.0 per package | see each package on nuget.org |
| [PawnIO](https://pawnio.eu) 2.2.0 — `PawnIO_setup.exe`, unmodified official signed release | kernel driver giving LibreHardwareMonitor access to CPU sensors; installed by our setup if missing | GPL-2.0 (driver), LGPL-2.1 (modules) | github.com/namazso/PawnIO, github.com/namazso/PawnIO.Modules, releases: github.com/namazso/PawnIO.Setup |
| .NET Runtime 10 (self-contained in the exe) | runtime | MIT | github.com/dotnet/runtime |
| [DSEG7 Classic](https://github.com/keshikan/DSEG) font | seven-segment digits in the window | SIL Open Font License 1.1 | github.com/keshikan/DSEG |
| [WiX Toolset](https://wixtoolset.org) 5 | building the installer (not shipped as a program) | MS-RL | github.com/wixtoolset/wix |

PawnIO is installed as a separate product (it has its own entry in *Apps and features*) and is not
removed when Jonsbo D34 Mon is uninstalled, because other monitoring tools may rely on it.

The screen protocol was worked out for interoperability from JONSBO's PC Monitor All. No JONSBO or
HWiNFO code or binaries are included.
