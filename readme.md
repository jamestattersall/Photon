##Photon - A minimal web application to access data copied from a Proton database.##

Requires an MSSQL databse in EAV format, copied from a Proton system.
See https://github.com/jamestattersall/Proton2Mssql

Photon consists of an html/javascript user interface to be viwed in a web browser and a .net core 10.0/Dapper web API.
The UI 

###Installation###
requires .net core 10+
provide connection string to the mssql database in appsettings.json

The backend server includes its own web server (Kestrel) so can be run directly from the command line (Photon.exe) or indirectly through IIS.
If using IIS, create a new web site or application pointing to the Photon folder.
Ensure the application pool is set to use .net core 10.0 and has permission to access the mssql database.
If using the recommended in-process model, the app would need to deployed without the single file option (single-file is not supported for in-process IIS)

###Usage###
The app layout (screen layout, captions, tables, fields, menus etc.) is informed by the metadata copied from Proton. 
Authentication is via windows authentication and user permissions taken from the Proton database on submission of an existing proton password.
After login, the user can view all data held in Proton, mostly with the same appearance and layout as in Proton itself. Fields and reports which depend on Quark (The reporting laguage built-in to Proton) are not accessible in Photon.
The app reponds to the same keyboard inputs as Proton as well as mouse clicks.

In addition to the Proton UI, Photon photon displays a graph of any visible numerical data (by clicking on the field in default mode).
Advanced mode exposes controls to search for any field, expose configuration details and select an altenative user-defined index or entity type.
