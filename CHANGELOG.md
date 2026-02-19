1.2.0
- Support for additional ODKG key algorithms and sizes
- Improved ODKG workflow to prevent outages caused by deleting in-use certificate before new one is installed
- Update Inventory Job to filter out non-certificate types
- Addressed memory leak potential (CWE-404)

1.1.2
- Update doc screenshots

1.1.1
- Updated integration-manifest.json to use 'BoschIPCamera' for capability and short name so it matches manifest and code references
- Renamed image files to reflect 'BoschIPCamera' cert store type shortname
- Removed bipcamera.md
- Removed TODOs from boschipcamera.md

1.1.0
- Added .net6/8 dual build
- Modifications to support doctool for README

1.0.4
- Initial Public release

1.0.3
- Initial Internal/Private release
- Supports Inventory and Reenrollment jobs
- Allows Certificate Usage codes:
    - None
    - `HTTPS`
    - `EAP-TLS-client`
    - `TLS-DATE-client`

1.0.2
- prototype build
