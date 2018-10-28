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
analytical properties will be transferred to Rhino accessible by the SOFiSTiK Rhino Interface there.
This means, the geometry baked within Rhino can be further edited and meshed to a SOFiSTiK analytical model from there - 
provided, of course, you have the SOFiSTiK Rhino Interface installed.
See the e.g. the [Online Documentation of the Rhino Interface](https://www.sofistik.de/documentation/2018/en/rhino_interface/index.html) for further information. 

Please note: the grasshopper assembly is continuously being developed and at this stage we cannot guarantee that, e.g.
node and parameter names may not change. However these changes mainly take place in the master branch.
For productive usage, we recommend to switch to a stable branch, available in parallel to the master, and to download the 
gha assembly from there. Stable branches may receive fixes on request but will not undergo larger changes.