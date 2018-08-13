# gh_sofistik

Project allowing to create a Grasshopper GHA assembly with components to convert Grasshopper geometry into SOFiSTiK input files.

Install the components by just dragging & dropping the assembly gh_to_dat.gha from the subfolder ./bin onto the Grasshopper window.

Currently, the assembly provides four components:

* SPT: allows to add SOFiSTiK structural properties to a point
* SLN: allows to add SOFiSTiK structural properties to a curve
* SAR: allows to add SOFiSTiK structural properties to a brep or surface geometry
* SOFiMSHC: creates input for the SOFiSTiK mesher SOFiMSHC from a list of items defined by SPT, SLN and SAR

