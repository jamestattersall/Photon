### Photon - A minimal web application to access data copied from a Proton database.

Requires an MSSQL database in EAV format, copied from a Proton system.
The importer that creates this copy is included in this repository, in the `ProtonImport` folder (see `ProtonImport/readme.md`). It was previously maintained separately at https://github.com/jamestattersall/Proton2Mssql.

This repository contains two projects, both built from `Photon.sln`:
- **Photon** - an html/javascript user interface viewed in a web browser, served by a .NET 11/Dapper web API.
- **ProtonImport** - a console application that reads a Proton database and copies its metadata and data into MSSQL.

### Installation
Requires .NET 11+.
Provide the connection string to the MSSQL database in appsettings.json.

The backend server includes its own web server (Kestrel) so can be run directly from the command line (Photon.exe) or indirectly through IIS.
If using IIS, create a new web site or application pointing to the Photon folder.
Ensure the application pool is set to use .NET 11 and has permission to access the MSSQL database.
If using the recommended in-process model, the app would need to be deployed without the single file option (single-file is not supported for in-process IIS).

### Usage
The app layout (screen layout, captions, tables, fields, menus etc.) is informed by the metadata copied from Proton.
Authentication is via Windows authentication (optionally with a network username and password, checked against Active Directory), restricted to the roles listed in `AuthSettings:AllowedRoles` in appsettings.json. User permissions are then taken from the Proton database on submission of an existing Proton password.
After login, the user can view all data held in Proton, mostly with the same appearance and layout as in Proton itself. Fields and reports which depend on Quark (the reporting language built into Proton) are not accessible in Photon.
The app responds to the same keyboard inputs as Proton as well as mouse clicks.

In addition to the Proton UI, Photon displays a graph of any visible numerical data (by clicking on the field in default mode).
Advanced mode exposes controls to search for any field, expose configuration details and select an alternative user-defined index or entity type.
