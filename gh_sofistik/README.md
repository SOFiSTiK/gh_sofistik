# gh_sofistik

Grasshopper components allowing to add analytical properties to Grasshopper geometry and convert this geometry into SOFiSTiK input files.

Install the components by dragging & dropping the assembly gh_sofistik.gha from the subfolder ./bin onto the Grasshopper window.
After installation a SOFiSTiK tab should appear in the control panel of Grasshopper containing four components:

* SPT: allowing to add SOFiSTiK analytical properties to a point
* SLN: allowing to add SOFiSTiK analytical properties to a curve
* SAR: allowing to add SOFiSTiK analytical properties to a brep or surface geometry
* SOFiMSHC: creates input for the SOFiSTiK mesher SOFiMSHC from a list of items defined by SPT, SLN and SAR

Following image shows an example:

![Creation of Girder System](https://github.com/SOFiSTiK/gh_sofistik/blob/master/gh_sofistik/examples/img/girder_system_01.JPG)

