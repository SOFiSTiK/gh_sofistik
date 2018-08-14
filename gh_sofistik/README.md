# gh_sofistik

Grasshopper components allowing to add analytical properties to Grasshopper geometry and convert this geometry into SOFiSTiK input files.

Install the components by dragging & dropping the assembly gh_sofistik.gha from the subfolder ./bin onto the Grasshopper window.
After installation a SOFiSTiK tab should appear in the control panel of Grasshopper containing four components:

* SPT: allowing to add SOFiSTiK analytical properties to a point
* SLN: allowing to add SOFiSTiK analytical properties to a curve
* SAR: allowing to add SOFiSTiK analytical properties to a brep or surface geometry
* SOFiMSHC: creates text input for the SOFiSTiK mesher SOFiMSHC from a list of items defined by the components SPT, SLN and SAR

Following image shows its usage together with the SOFiSTiK analytical model generated from the text input in Grasshopper:

![Creation of Girder System](https://github.com/SOFiSTiK/gh_sofistik/blob/master/gh_sofistik/examples/img/girder_system_01.JPG)

The components SPT, SLN and SAR for generating SOFiSTiK structural entities also support 'Baking' such that the defined 
analytical properties will be transferred to Rhino and accessible by the SOFiSTiK Rhino Interface.
This means, the geometry baked within Rhino could also be directly meshed and exported to a SOFiSTiK analytical model from there - 
provided, of course, you have the SOFiSTiK Rhino Interface installed.
See the e.g. the documentation of the Rhino Interface for further information: [](https://www.sofistik.de/documentation/2018/en/rhino_interface/index.html)
