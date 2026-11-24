
# Application to create and maintain copy of live Proton database in MSSQL.

The app should be installed on a windows PC or server with access to MS SQL. 

Depends on .net core 10.0

Execute the protonConsole2.exe app from a command prompt. Include an arbitary command line argument to force display of the settings interface.
The settings interface will be shown anyway if there are missing or invalid settings in appsettings.json.

The proton database consists of several files all with the file extension .dbs and located in the same directory on the Proton unix server.
These files should be copied into the same direcory on a Windows server, accessible to this app or, ideally,  onto the same machine as the app..
The files include base.dbs, entity.dbs, data.dbs, item.dbs. dict.dbs, code.dbs, screen.dbs, scrtext.dbs, index.dbs, indexdef.dbs, trgroup.dbs.
.

The app presents a console with options to set the settings required to create the SQL connection string and the path to the Proton .dbs files.
The settings are stored in the application root/appsettings.json. Integrated security is used for SQL connection to avoid storing passwords in the app. 

When the settings have been entered, the option load/update metadata creates the SQL database and populates the SQL tables with the metadata required to interpret the proton data. This metadata includes screen layouts, field names, captions, menu structures, encrypted passwords etc.

When the metadata has been loaded, the option to load/import data will copy all of the data held on Proton into the SQL tables. This data includes, free text, associated entities (e.g. GPs, staff, dialysers, locations etc) as well as all patient data.

# SQL data structure

The data copied into SQL is held in Entity, Attribute, Value (EAV) format. The attributes are the Proton Items, defining the fields (name, data type, display format etc.). The entities are the individual patients, staff, locations, GPs etc. When the data is displayed in a 2-dimensional grid, the attrubute names are the column headers and the values are the contents of the grid cells. The rows of the grid could be dates for time-related data (e.g. a table if laboratory results for a selected patient) or could represent individial entities (e.g. patient identifiers in a report listing information on a range of patients).

The individual values are stored in datatype-specific value tables (ValueNumbers, ValueTexts, ValueDates etc.).

The metadata consists of information to define the tables, views, menues etc.

For more information on the EAV format, see the following article: https://pmc.ncbi.nlm.nih.gov/articles/PMC2110957/

The structure of the data in Proton is similar to the EAV format with it's relations/tables broadly conforming to the EAV tables as follows:
|Proton .dbs files		|EAV SQL tables                                                                                      |
|-----------------------|----------------------------------------------------------------------------------------------------|
|patsts.dbs             |Entities                                                                                            |
|entity.dbs             |EntityTypes                                                                                            |
|Item.dbs, valid.dbs    |Attributes                                                                                          |
|codes.dbs, dict.dbs    |Lookups                                                                                             |
|codedef.dbs            |LookupTypes                                                                                         |
|(implied)				|DataTypes                                                                                           |
|Data.dbs			    |Values (datatype-specific) (EntityId INT, AttributeId SMALLINT, Seq INT, + datatype-specific value field)|
|						|-ValueNumbers (float)                                                                             |
|						|-ValueTexts (VARCHAR(255))                                                                        |
|						|-ValueTimes (time)                                                                                |
|						|-ValueLookups (int)                                                                               |
|						|-ValueEntities (int)                                                                              |
|frtext.dbs 			|-ValueLongTexts (VARCHAR(MAX))                                                                    |

The value tables have a compound primary key (EntityId INT, AttributeId INT, Seq INT).
Seq (sequence) is the 1-based ordinal row number.These non-EAV-standard keys are retained for compatibility with the Proton data structure.
Ideally, the primary key should be (EntityId BIGINT, AttributeId SMALLINT) only, and table rows would be entities with attributes ParentEntityId (e.g. patient instance), EntityTypeId (e.g. Blood chemistry), Date and Seq but this would complicate population from the source data in Proton..

**concepts specific for proton and retained for compatibility**
|Proton .dbs files		    |EAV SQL tables         |
|---------------------------|-----------------------|
|screen.dbs			        |Views, ViewAttributes|
|scrtext.dbs			    |ViewCaptions|
|Menu.dbs                   |Menus, MenuItems|
|trgroup.dbs                |Tables, TableAttributes|
|passwd.dbs                 |UserStarters|
|Index.dbs                  |Indexes|
|IndexDef.dbs, Keydef.dbs   |IndexTypes|

In order to facilitate querying the EAV database the following table-valued functions are provided:
GetViewValues(@entityId INT, @viewId INT @page)
GetTableValues(@entityId INT, @tableId, @page)
To return data in spreadsheet format, the following sps are provided:
TableValuesCrosstab(@entityId INT, @tableId INT, @startRow INT, @rows INT)
ViewValuesCrosstab(@entityId INT, @tableId INT, @startRow INT, @rows INT)



# Proton database file structure
Proton is a clinical information system developed by CCL in the 1970s for use on minicomputers running the Unix operating system. Proton has been used extensively in the UK National Health Service (NHS) since the early 1980s, particularly in renal medicine. Proton systems are used to manage clinical data for dialysis patients, including laboratory results, clinical notes, prescriptions, dialysis session records etc. Proton is still widely used in the NHS and other healthcare systems worldwide.
In Proton, patient data is stored in a custom database format designed specifically for healthcare data. Proton impliments features which make is especially suitable for healthcare data, including efficient storage of sparsely-populated data fields, easy addition of new data fields and flexible configuration to meet the needs of different clinical departments. Configuration can be changed without breaking existing data. Healthcare IT systems in current use tend to use relational databases which larley lack these advantages.

The Proton database schema is largely compatible with the EAV format (Entity Attribute Values). Patient data is stored in just two ‘value tables’ (data.dbs, frtext.dbs). Supporting 'metadata tables' are required to define how the stored binary data is interpreted, accessed and displayed on screen (item.dbs, screen.dbs, trgroup.dbs menu.dbs etc.). The technical advantage of the EAV structure is that few 'tables' are required and healthcare data (which requires a large number of sparsely-populated fields) is stored efficiently. From the user's point of view, the EAV structure works well because it is easy to add and edit fields, entities, views etc. to accommodate changing clinical practice.

Proton was developed in the 1970s when hardware was extremely limited and a database could hold only one table. To meet the requirements for multiple tables, proton accesses multiple databases, each held in a separate file with the .dbs extension, located in the dbs directory.

A typical Proton system might contain data for 50,000 patients, 2000 sparsely-populated fields and 100,000,000 non-null data values. Such a Proton database would require approximately 1GB storage. The same data would require about 10 GB in a relational database.

Proton systems are adapted or 'confugured' to the requirements of different clinical departments by local support staff. These 'configuration' changes are made by changes to the logical schema which is stored as metadata in the Proton database. The 'configuration' changes do not change the physical schema (definition of tables and fields) of the database or the code in the Proton application. This contrasts with a modern relational database where logical schema changes are effected by changing the physical schema and, necessarily, the code accessing the data.

Wherever possible, data is stored in a sequential chain so that it can be read from the database in the order in which it is required to display on screen. Data within a database is stored in pages of fixed length (generally 64, 128, 256, 512 or 1024 bytes) designed to fit into the main processor RAM. A page is also known as a record and will hold related data (somewhat similar to a table row). The record/page ID is not stored but inferred from the ordinal position of the page within the database file starting with 1. A record/page can be accessed quickly by advancing the file pointer to (\[record ID\] -1) \* \[page length\] and reading \[page length\] sequential bytes. In this respect, the page/record ID is, in effect, a primary unique key with a clustered index.

Base.dbs holds the name of each database, their page lengths, number of pages, start of free space in database and maximum number of pages allowed.

A page/record consists of a series of bytes. Data values are stored at specified locations within the record. These locations are identified by the offset in bytes from the start of the page (starting at 0). The location of data values (page offsets) is somewhat similar to the modern concept of a field in the physical schema.

An array of values may be stored as a series of grouped bytes known as data blocks. A block may contain a group of values. The individual value within the block is accessed at a specific offset relative to the beginning of the block. The ordinal positions of the blocks within a series acts as an indexed ID for the block. The first data block would be accessed at a specified page offset. Subsequent blocks can be accessed with knowledge of the block lengths and their ordinal positions within the series.

To make things a bit more difficult, some databases contain series of data blocks of variable length. In this case, the block length is either stored as one of the values in the block or there is a delimiter at the block boundary. In either case, individual blocks can only be accessed by iterating block by block from the start of the series. Each block must be evaluated in turn (at least finding the delimiter or block length value). Proton stores data in the order in which is required to display on screen so this is not a severe limitation.

Where a series of data blocks may overflow a page, they are stored in a chain of as many pages as required. In this case, the first few bytes on the page (the header) are pointers to the next (and sometimes previous) page in the chain. These page pointers are integers evaluating to a page/record ID. Pages in a chain are not stored sequentially but are linked by the next page pointer.

The physical structure of the proton database (organisation of data within databases, their pages, page chains, page offsets etc.) must be known in order to find and evaluate any stored value. In a modern database, this organisation can be deduced by inspecting the database physical schema (tables, fields, relationships, indexes etc.), which generally has a graphical interface. In Proton, some of this can be deduced by inspecting the Proton metadata (e.g. base.dbs), but much of this organisation is hard-coded within the Proton application. Proton includes binary/hex editors which allows a user to inspect the raw data in the proton databases. By comparing the raw data in the .dbs files with values presented by the Proton application, it is possible for a user to deduce the physical structure and schema of Proton.

You can inspect (and edit) the live Proton database files using the data editor built in to Proton. Editing the live data files is almost certain to damage the system. It is easier and safer to use a free binary/hex text editor such as HxD on read-only copies of the live Proton databases.

<http://mh-nexus.de/en/> 

If you use HxD, open a copy of base.dbs as read-only, then set the View/bytes per row to 64, then View/offset base decimal.

You will see the database as a spreadsheet with one row per record (since the page length of BASE.DBS is 64 bytes). The top row has database name “BASE.DBS” in the first column (scroll to the right to see the text conversion of the raw data).

The columns are at specific offsets (e.g. for BASE.DBS, offset 24-25 is the page length, you will find the value &0040 (=64 in decimal) at this offset for the DATA.DBS row).

# Proton concepts

## ID Line

An 80 x 1 character banner across the top of the Proton screen. The ID line identifies the currently selected entity (class) instance. If a patient is selected, the ID line would consist of the patient's name, hospital number, date of birth etc. The items displayed on an id line are defined as a screen (in screen.dbs) and the record/page ID of the ID line screen is stored as the default ID line (in entity.dbs).

## Entity

The Proton entity is similar to a class in modern programming terminology. There is always an entity for the patient. Additional entities are created as required and could be staff member, Hospital location, dialyser, equipment, General Practitioner. Each entity defines a template for data structure The Proton database would store multiple instances of one or more entities. A patient called John Smith would be stored in Proton as an instance to the patient entity.

Each entity has a single specified item as a unique indexed free-text identifier (e.g. hospital number). This identifier is used as a primary ID for the patient for external purposes. This is unfortunate because it can be changed by a user. The genuine primary key, used by proton for internal purposes, the entity instance ID would be a better choice as the entity key. The entity instance ID can be accessed by a Proton quark us the “patient” system variable. An entity can be selected in Proton as # followed by the entity instance ID.

This identifier can be stored in a record of another entity instance as an entity key.

Entities can be linked together (e.g. a patient's GP) by storing the entity key for one entity instance (e.g. a specific GP) in the record of another entity instance (e.g. a specific patient).

An entity also has a default ID line..

## Item

The Proton Item is the Attribute in EAV terminology. The item is roughly equivalent to a field/column of a table in a relational database. The item defines the meaning of any values describing an entity instance. In Proton systems there are typically about 2000 items. For the patient entity, there may be items last name, first name, surname, date of birth etc.

The item describes the data type (test, integer, code etc.) display length, the entity it describes and its (optional) membership of a group.

## Screen

The Proton screen is an 80 x 18 character area for displaying data for the selected entity and screen (view).

The screen consists of a number of fixed captions and place-holders for stored data. The coordinates of the captions and place-holders, the text of the captions and the Ids for the items defining what is displayed in the place-holders is stored in metadata (SCRTEXT.DBS and SCREEN.DBS).

## Data type

Each attribute/item has a DataTypeId as a byte 1-12. This identifies the data type for the item. These include unsigned or signed integers in 8,16 or 32 bit lengths, Fixed and variable-length free text stored as 8-bit ASCII to maximum of 128 characters, IEEE floating point numbers in 32 and 64 bit lengths, coded lists stored as 16 and 32-bit unsigned integer Ids, entity keys stored as variable-length strings of 8-bit ASCII characters to maximum length of 128 characters and long text (memo) stored as 8-bit ASCII which cannot be indexed but can be any length.

## Static data

These are attributes/items which are direct timeless unchanging properties of the entity instance (e.g. date of birth is a static property of a patient).

For static data, there can be only one value stored per entity instance.

Changes to static data overwrite the previous version without preserving it. Ideally, there should be no static data as clinical record guidelines require the facility to correct values while preserving previous versions. A typical Proton system contains a significant amount of static data as early versions (pre- 1980) lacked the capability to store historical (time/related) data.

Static data items are defined in items.dbs with empty values for 'date item' and 'group'.

## Time related (T/R) data

Time related (T/R) data are attributes/items which are members of a defined group which includes a key date (the key date item). T/R data is always linked to a key date as well as an entity instance. There can be many values of the same attribute/item. An example would be systolic blood pressure which is measured multiple times for an individual patient.

T/R data is grouped together in a T/R group. The T/R group consists of multiple items which share a single key date item and entity. An example would be T/R group “Haematology tests” consisting of key date item “date of test” and items for “haemoglobin”, “white cell count”, “platelet count” etc.

The tr/group can be considered as a table in a logical schema, with items as columns and key dates as rows.

T/R data would normally be displayed in Proton in spreadsheet format, in descending date order, one entity instance (e.g. patient) per spreadsheet, one key date value per row and one item per column. The items to be displayed as columns, the column order and the position on screen is defined and stored as metadata (in screen.dbs). The column headers are defined as caption text and stored as metadata (in scrtext.dbs). A peculiarity of Proton is that the same items can have captions which vary between screens/views and there is no explicit link between caption and item. So the item for systolic blood pressure could be allocated a caption “Weight” on one screen and “BP” on another.

T/R items are defined in items.dbs as non-empty values for 'date item' and 'group'. The 'date item' is the page/record ID in item.dbs for the key date item. The item group is a page/record ID for either screen.dbs or trgroup.dbs. All items which are members of the same t/r group would share the same 'group' and 'date item' values.

To be displayed correctly in a spreadsheet view, all values must be displayed in the correct column and row. In Proton, values are stored in data.dbs together with the item which defines the column explicitly. The row ID is not stored. It is inferred from the ordinal position of the value within a series of stored data. This means that if a value is deleted, data following in series moves to a higher ordinal position and will be associated with the wrong row. For this reason, a blank ‘filler’ must be stored in place of deleted data to preserve the row Ids. Similarly, if a value is inserted, all other columns in the group must be ‘balanced’ by adding blank data at the same ordinal position for every other item/column in the group.

## Database

A Proton database is stored in a single file (e.g. base.dbs) and is roughly equivalent to a single table in the modern concept of a physical database schema.

## Record

A Proton record is a contiguous sequence of bytes within a database. The record is also known as a page. The record/page has a defined length in bytes (defined and stored in base.dbs) specific for the database. The record is very roughly equivalent to a table row in a physical schema.

## Record ID

A proton record has an ID which is a 32-bit integer. This ID is not stored but implied as the ordinal position of the page/record within the Proton database. The ID is 1-based, (the first record has ID=1)

The ID can be used to find the start of the record rapidly by advancing the file pointer to byte position (ID -1)\*page length in bytes.

## Dictionary codes

The dictionary codes (or DICT codes) are lookup values. They are often defined by the system administrator and do not correspond to any standard code definition (e.g. SNOMED). The DICT codes are stored in records as 16-bit integers values evaluating to the record ID in DICT.DBS. The text to be displayed is stored in the DICT.DBS database. A limitation of DICT codes is that there is no provision for associated data (e.g. standard code, code type) and there is a limit of 65535 codes. When entering DICT codes at the Proton user interface, a list of the subset of possible DICT codes is presented to the user. This subset is defined as a range of DICT record Ids valid for the item defined in the validation metadata (in valid.dbs). A limitation of this grouping is that all codes to be displayed in a specific list must have contiguous record Ids (so must be stored in contiguous locations in DICT.DBS).

## New (READ) Codes

To overcome the limitations of DICT codes, and to accommodate READ V2, OPCS and ICD10 codes. The new codes were introduced in around 1985. The new codes are also known as READ codes, even when they are used to store codes in coding systems other than READ. Like DICT codes, the new/READ codes may be maintained by the system administrator and may not correspond to any standard code system. In the code editors provided in Proton, the Read codes are named the Code dictionary, which sometimes causes confusion.

New/READ codes are stored as 32-bit integers, evaluating to the page/record ID in READ.DBS. The record in READ.DBS contains the code text, code type ID (e.g. evaluating to diagnosis) and an indexed 5-character code in the same format as READ V2, OPCS or ICD10.

The 5-character code index allows hierarchical browsing of the code records, appropriate for the hierarchical structure of READ V2 and ICD10. In these code systems, the hierarchy is explicitly hard-coded in the code (e.g. code A2e.. is a child of A2...). The hierarchical browsing of codes in Proton will not work for the current version of READ (V3) or SNOMED CT, where the hierarchy is defined in a separate database. SNOMED CT codes are longer than 5 characters so cannot be used in Proton (unless the code is embedded in the Code text).

The code type allows codes to be grouped independently of the code record/page ID. So the codes do not need to be grouped in contiguous pages in READ.DBS.

# Proton data types

StringVar: variable length sequence of 8-bit ASCII characters, terminated by empty byte

String(n): Sequence of n 8-bit ASCII characters no terminator. Can contain a shorter sequence if terminated by empty byte.

Numbers: Proton stores numbers in big endian format.

INT8,INT16,INT32: Signed 8, 16 or 32-bit integer.

UINT8,UINT16,UINT32: Unsigned 8, 16 or 32-bit integer.

FLOAT32, FLOAT64: IEEE 754 floating point numbers

CODE: UINT32 interpreted as page ID in CODE.DBS (READ code), display the code text defined in CODE.DBS

DICT:UINT16: interpreted as page ID in DICT.DBS (Dictionary code), display the code text defined in DICT.DBS

COMPOSITE FLOAT64:

Bytes 0-7: IEE 754 floating point double precision,

Bytes 8-9 UINT16 evaluating to CODE qualifier type 1= prefix, 2=replacement text

Bytes 12-15 UINT32 evaluating to CODE qualifier (e.g. “&lt;”, “&gt;”)

COMPOSITE FLOAT32:

Bytes 0-3: IEE 754 floating point single precision,

Bytes 4-5 UINT16 evaluating to CODE qualifier type 1= prefix, 2=replacement text

Bytes 8-11 UINT32 evaluating to CODE qualifier (e.g. “&lt;”, “&gt;”)

DATE: UINT16 as number of days since 01/01/1860

TIME: UINT32:

IF &H20000000 interpreted as string “PRE”

IF &H40000000 interpreted as string “POST”

IF &H80000000 interpreted as string “0000”

otherwise as number of milliseconds since midnight

ENTK: StringVar interpreted as unique identifier to an entity instance

FTXTPTR: UINT32 interpreted as start page for chain in FTXT.DBS (free/long/memo text)

Blob or binary data types are not supported in Proton.

# Schema of Proton databases

In the following text, Byte refers to a byte located at the specified page offset. Bytes refers to a series of bytes starting and ending at the specified offsets (inclusive). The offsets are relative to the start of the page. The first byte on the page has offset 0. This zero-based offset is to be compatible with standard binary/hex editors.

Pitfalls:

1. Page record IDs are not stored but are implied from the ordinal position of the page within the .dbs file. Proton considers these virtual IDs as 1-based (the page at the beginning of the file is considered by Proton to have ID=1). Offsets within pages and blocks in this document are zero based to be compatible with binary/HEX editors.
2. Proton numbers may be stored in big-endian (high-byte first) or little-endian format (low byte first). Windows systems running on Intel processors are little-endian. The endian-ness of the Proton machine needs to be taken into account when evaluating numbers stored in two or more bytes. The .net core framework has classes which perform the conversions.
3. Text strings in proton may be terminated by an empty byte &00. This may be interpreted as NULL and make the string invalid on modern operating systems (including Windows). You have to remove trailing &00 from strings.
4. Proton uses an empty byte &00 as a line break. This will make the string appear as invalid on Windows systems. Replace &00 inside strings by &0A (linefeed).
5. There is an algorithm to confuse anyone trying to use the built-in index (INDEX.DBS).

## BASE.DBS

Page chains allowed:no

Base or configuration data for all the databases used by Proton.

Bytes 0-15: StringVar: DB filename

Bytes 24-25: UINT16: page length

## CODEDEF.DBS

Page chains allowed:no

New/READ code group type.

Bytes 64-144: StringVar: name of code type (e.g. “Diagnosis”, “Sex”)

## CODES.DBS

Page chains allowed:no

New/READ Code lookup.

Bytes 0-79: StringVar: code display text (e.g. “Diabetes”, “Male”)

Bytes 80-81: UINT16: code type ID

Bytes 84-88: String(5): READ code (e.g. “Ad123”)

## DATA.DBS

Page chains allowed:yes

The EAV Values table. Stores all of the entity property values apart from long text. The actual data values are stored in a series of data blocks.

All data for an entity instance is stored within a single page chain, in ascending order of item ID. The page at the start of the chain for a specified entity instance and attribute/item is defined in VRX.DBS. The specified entity instance ID is the ID VRX.DBS page/record ID. VRX also includes pointers to the first page containing specified items. Use VRX to find the first page/record in data.dbs likely to include the item value required. Then iterate and evaluate data blocks one by one until the required item is found.

It is possible to find the start of the chain without VRX.DBS by iteratively reading records in DATA.DBS until a record containing the desired entity instance ID is found at location page offset 8, then following the chain backwards until the root record is found. This is expensive on processing time. Proton uses this method to rebuild VRX.DBS if it gets corrupted.

Pitfalls:

1. Bytes in data blocks evaluating to numeric or ID may be truncated at the rightmost end (least signifcant on big-endian systems). This is to save space by not storing trailing zeros. A small integer stored as a float would have a lot of trailing zeros in IEEE 754 coding. To evaluate bytes as numbers, you need to right pad the stored byte series by adding zeros to the required length for the datatype (defined in item.dbs). The redundant most-significant leading zeros in integers and IDs are not removed.
2. FLOAT64 numbers may be composite and include appended bytes to be evaluated as READ codes. These appended bytes are subject to least significant truncation of zeros so you cannot rely on specific values for block length to detect appended READ code Ids. FLOAT64 blocks longer than 11 bytes for non-repeating block or 12 bytes for repeating blocks will include appended codes. See spec for Composite FLOAT64
3. TIME values may be codes evaluating to “PRE”, “POST”, “0000” or an integer evaluating to number of milliseconds since midnight. See spec for TIME datatype.
4. Time-related data values must be linked to the correct row and key date. This linkage is via a row ID which is not stored explicitly. Row ID is the ordinal position of the value relative to the first value for the item. Values are stored in descending order of row ID. Key dates are stored in descending date order. Data blocks have varying length so there is no way to deduce the row ID without evaluating and counting each block in series. All empty and repeating values must be counted. The count must be continued over page boundaries.

Bytes 0-3: UINT32: Next page ID: the page next in chain

Bytes 6-7: UINT16: Number of free bytes at end of page

Bytes 8-11: UINT32: Entity instance ID: Page/record ID in PATSTS.DBS and VRX.DBS: All values on page relate to same entity instance (e.g. specific patient). All pages in the chain should have the same value for entity instance ID.

Bytes 12-13: UINT16: High item ID: The page in ITEM.DBS, the maximum item number on page

Byte 15: UINT8: Type of data stored on page

Bytes 16 to page length-1: Series of variable length Data blocks

x=start of block, block length read from bits 1-7 of byte x+0

Bytes x+0 to x+1: UINT16: Item ID, key to page in ITEM.DBS & VALID.DBS

Byte x+2: composite data value byte length and repeat flag

Bit 0 is repeating data flag,

bits 1-7 UINT8: total length of data block.

If Bit 0=0, Bytes X+3 to X+total length-1 is data value, do not repeat

If Bit 0=1, Bytes X+3 to X+total length-2 is data value, Byte X+total length-1 is number of times to repeat value.

Data value to be interpreted as datatype defined in ITEM.DBS

May require validation using data in VALID.DBS

Byte X+total length is start of next data block

## DICT.DBS

Custom code lookup

Page chains allowed:no

Bytes 0-63: StringVar: Code display text (e.g. “Main hospital”)

## ENTITY.DBS

Entity type/class (e.g. Patient, GP, location, drug)

Page chains allowed:no

Bytes 0-15: StringVar: name of entity (e.g. “Patient”)

Bytes 16-17: UINT16: Screen ID of banner (screen grouping identifying banner items)

Bytes 18-19: UINT16: Item ID of unique identifier (e.g. hospital number)

## FRTEXT.DBS

Free/long text/memo. All text stored as lines of StringVar. Empty byte &00 is line break.

Text can be of any number of lines, may span multiple pages. Each text record stored in a single page chain. The page number of the start of the page chain is stored as a record of type FRTXT in DATA.DBS.

Pitfalls:

1. Exclude terminating empty bytes &00.
2. Replace the &00 characters inside text with &0A (line break).
3. The text will be formatted for a limited hardware VDU 80 characters wide. In a modern display the lines will appear strangely short.

Bytes 0-3: UINT32: Number of the next page in chain

Byte 7: UINT8: Total number of lines on page.

Bytes 32 to page length -1: Variable-length data blocks. Blocks delimited by empty byte &00. Each block evaluates to a single StringVar, line of text.

## IDXDEF.DBS

The definitions/configuration of the built-in entity index INDEX.DBS

Allow page chain: no

Bytes 0-1: UINT16: Key length: The length in bytes of the key (NEEDS TO BE INTERPRETED USING ALGORITHM).

Byte 2: UINT8: partial match flag – if &01 match at start of search string, if &00 match entire string.

Bytes 4-7: UINT32: Seq1 - the ID in INDEX.DBS start page to search from beginning

Bytes 8-9: UINT32: Root - the ID in INDEX.DBS start page to search from middle

Bytes 16-17: Screen ID for ID line identifying match entity hits.

## INDEX.DBS

The built-in indexes for finding/selecting entity instance. Used to retrieve the entity instance ID using an identifying string (e.g. hospital number, name etc.).

Allow page chain: yes

Pitfall:

1. There is an algorithm embedded in Proton which is required to read the index. This was required for compatibility with various systems.

You need the length of the key in order to iterate through the keys (there are no stored delimiters, key length or explicit key Ids). You need the following algorithm to get the correct key length from IDXDEF.dbs, bytes 0-1 (KeyLength).

Block length = 4 \* ((KeyLength + 11) \\ 4

An index consists of a sorted list of strings used as keys to entity instance (e.g. patient name, hospital number)

Each Index occupies a single page chain, as many pages in chain as required. The start page for the chain is stored in IDXDEF.DBS.

To use the index, find the start of chain and block length using IDXDEF.DBS (and correction algorithm), load the first record in the chain from INDEX.DBS, read each key in turn until you have a match on the key text, read the associated entity instance ID.

Multiple keys are stored on page.

Bytes 0-3: UINT32: Previous page in chain.

Bytes 4-7: UINT32: Next page in chain.

Bytes 12-13: UINT16: Number of keys in page.

Bytes 14-15: UINT16: Index ID, page in IDXDEF.DBS

Bytes 20 to page length-1: Fixed length data blocks containing the keys.

Block length = key length defined in IDXDEF.DBS with correction algorithm applied.

x=block start

Byte X+0 to X+3: UINT32: Entity instance ID, page in PATSTS.DBS, VRX.DBS

Byte X+4 to Block length-1: String(Block length-5): The key text

X+Block length : the start of the next key.

## ITEM.DBS

The EAV attributes.

Allow page chain: no

Bytes 0-5: String(5): the name of the item.

Bytes 6-7: UNIT16: Data type;

1=String

2=INT8 or UINT8 depending on value in subtype

3=INT16 or UINT16 depending on value in subtype

4=INT32 or UINT32 depending on value in subtype

5=FLOAT32 (single)

6=FLOAT64 (double)

7=DICT

8=DATE

9=TIME

10=FRTXT

11=ENTK, entity type dpepending on value in subtype

12=CODE, code type depending on value in subtype

Bytes 8-9: UINT16 sub type

Bytes 10-11: UINT16: Display length in bytes/characters

Byte 12: UINT8: Flag 1 (isInstalled=bit7, iscalculated=bit6)

Byte 13: UINT8: Flag 2 (isIndexed=bit0, isMandatory= bit1, CanDuplicateIndex= bit2)

Bytes 14-15: UNIT16 :Group: 0=static (patient property), >0 grouped with other attributes

Bytes 16-17: UNIT16 :Date Item ID: key date for group

Bytes 18-19: UNIT16 :entity ID:

Bytes 20-37: StringVar :comment/ description

## KEYDEF.DBS

The definitions/configuration for the keys used to search or select entities

Bytes 0-1: UINT16: INDEX ID – the page in IDXDEF.DBS

Bytes 2-3: UINT16: Item1: item ID for the first item to match: the page in ITEM.DBS

Bytes 4-5: UINT16: Item2: item ID for the optional 2<sup>nd</sup> item to match: the page in ITEM.DBS.

Byte 9: UINT8: Key length

Bytes 14-15: UINT16: Entity ID the page in ENTITY.DBS.

Bytes 16-end of page: StringVar: The name of the key (e.g. patient name)

## MENU.DBS

The menus used for navigation in Proton.

Allow page chain: no

Bytes 0-19: StringVar: Menu title (normally displayed at the top left of the menu area)/

Bytes 20-page length-1: Series of 9 Fixed length data blocks. Block length = 32 bytes.

x=start of block

Bytes x+0 to x+11: StringVar: Item title.

Byte x+12: UINT8: flag

Byte x+13: UINT8: Privilege – display item only if <= Privilege defined in PASSWD.DBS

Byte x+14: UINT8: Priv2

Byte x+15: UINT8: Function

Bytes x+16 to x+17: UINT16: P1 (optional parameters)

Bytes x+18 to x+19: UINT16: P2

Bytes x+20 to x+21: UINT16: P3

Bytes x+22 to x+23: UINT16: P4

Bytes x+24 to x+25: UINT16: Start menu ID

Bytes x+26 to x+27: UINT16: Next menu ID

x+32: the start of the next menu item block

## PASSWD.DBS

Passwords, not particularly secure, but control start menu for users

Allow page chain: no

Bytes 0-11 : StringVar password encrypted (character code XOR 31 to decrypt/encrypt).

Bytes 12-13: UINT16: function (0=menu, 83=display screen, 82=run quark)

Bytes 14-15: UINT16: parameter ID (e.g. menu, screen , quark )

Bytes 16-17: UINT16: privilege (value to determine which menu options displayed)

Bytes 18-19: UINT16: entity ID

Bytes 20-21: UINT16: ID line (screen number of entity banner)

Bytes 22-26: StringVar: USER NAME 

## PATSTS.DBS

The entity (object) instances
The ID/page number is a 1:1 link to ID/page in VRX.DBS.

Allow page chain: no

Bytes 32-35: TIME: no of milliseconds since midnight on day of latest update
Bytes 36-37: DATE: latest date of any updates

## SCREEN.DBS

The templates for displaying data on screen.

Allow page chain: no

Also used to group attributes/items as columns on a spreadsheet view with each row tied to a key date and attribute/items as columns.

Header length: 48 Bytes, Data block length 10 Bytes.

Bytes 0-23: StringVar: Screen title (optional).

Bytes 26-27: UINT16: N items – the total number of items displayed on screen.

Bytes 28-29: UINT16: N rows – the number of repeating date-keyed values displayed below each item.

Bytes 48 to page length-1: Data blocks, fixed length 4 bytes.

x=start of block

Byte x+0: UINT8: x coordinate on screen in character offset from left edge.

Byte x+1: UINT8: y coordinate on screen in character offset from top edge.

Bytes x+2 to x+3: UINT8: item ID – key for page in ITEM.DBS

Byte x+4: the start of the next block

## SCRTXT.DBS

Any fixed text (captions, titles) to be displayed on screen.

Allow page chain: no

ID is the same as the page in SCREEN.DBS

No header: variable length data blocks delimited by Byte &05.

Bytes 0 to page length-1: Variable length data blocks, delimited by Byte &05

x=start of block

Byte x+0: the block start delimiter Byte &05 (not present on the first block)

Byte x+1: x coordinate on screen in character offset from left edge

Byte x+2: y coordinate on screen in character offset from left edge

Bytes x+3 to next occurrence of Byte &05: StringVar: text to be displayed screen.

next occurrence of Byte &05: the start of the next block

## VALID.DBS

The input validation for attributes/items. The ID/page number is a 1:1 link to ID/page in ITEM.DBS.

Allow page chain: no

Field definitions depend on item datatype

DataType=2:

Byte 33: UINT8/INT8: minimum allowable value,

Byte 41: UINT8/INT8: maximum allowable value,

Datatype=3

Bytes 32-33: UINT16/INT16: minimum allowable value,

Bytes 40-41: UINT16/INT16: maximum allowable value,

Datatype=4:

Bytes 30-33: UINT32/INT32: minimum allowable value,

Bytes 38-41: UINT32/INT32: maximum allowable value,

Datatype=5:

Bytes 40-33: FLOAT32: minimum allowable value,

Bytes 48-41: FLOAT32: maximum allowable value,

Bytes 12-13: UINT16: Qualifier code type

Bytes 16-17: UINT16: Modifier code type

Datatype=6:

Bytes 40-33: FLOAT64: minimum allowable value,

Bytes 48-41: FLOAT64: maximum allowable value,

Bytes 12-13: UINT16: Qualifier code type

Bytes 16-17: UINT16: Modifier code type

Datatype=7

Bytes 32-33: DICT: minimum allowable value,

Bytes 40-41: DICT: maximum allowable value,

(NB, this specifies a contiguous range of allowable codes)

DataType=12

Bytes 32-33: UINT16: The CODE TYPE

## VRX.DBS

The main index for locating values within data.dbs for a specified entity instance (e.g. patient).
The ID/page number is a 1:1 link to ID/page in PATSTS.DBS.

Allow page chains: no

The record/page ID is the entity instance ID (the primary key for an individual patient, GP etc.).

Bytes 0 to page length -1: Fixed length data blocks. Block length = 8.

x=start of block

Bytes x+0 to x+1: UINT16: Max Item ID: the highest item ID stored on page in data.dbs

Bytes x+2 to x+3: UINT16: chain length: number of pages in data.dbs chain before reaching the page pointed to in the next block.

Bytes x+4 to x+7: UINT32: Pointer to page in data.dbs

x+8 : start of next block

To find the first page in data.dbs page chain containing data for a specified entity instance:

1. Find the entity instance ID by finding an identifying key in INDEX.DBS. Proton application will give you this ID for a selected patient using the quark system variable “patient”.
2. Access the record with the same entity instance ID in VRX.DBS.
3. Read the page ID as a 4-byte integer from offset 4 (first data block).

To find the value of as specified item (e.g. date of birth) for a specified patient.

1. Get the entity instance ID for the patient as above
2. Access the record with the same entity instance ID in VRX.DBS.
3. Iterate and evaluate the first 2 bytes as UINT16 (Max item ID) in each block on the VRX.DBS page until max Item ID is >= the specified item.
4. Read the page ID from bytes 4-7 (the last 4 bytes) in the block evaluated as UINT32 (page pointer).
5. Access the page in DATA.DBS with the page ID read from VRX.DBS.
6. Iterate and evaluate each data block on the DATA.DBS page until the item ID (bytes 0-1 of the block) matches the specified item. continue to the next page in chain if required.
7. Read the data value from the data block (bytes 3 to (end of block or end of block-1 if bit 1 of block set) and interpret according to datatype defined in ITEM.DBS.
