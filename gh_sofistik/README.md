# gh_sofistik

Grasshopper components allowing to add analytical properties to Grasshopper geometry and convert this geometry into SOFiSTiK input files.

Install the components by dragging & dropping the assembly gh_sofistik.gha from the subfolder ./bin onto the Grasshopper window.
After installation a SOFiSTiK tab should appear in the control panel of Grasshopper containing four groups of components:

* General: provides a component to stream data to a SOFiSTiK input file
* Geometry: provides currently one auxiliary component to create a SOFiSTiK axis definition (SOFiMSHC: GAX) from curve geometry
* Structure: provides components to convert points, lines and areas to SOFiSTiK structural elements. These elements can be then 
  passed to an additional component which allows to convert the structural element information into SOFIMSHC input.
* Loads: provides components to add loads to points, lines and areas. As input, Grasshopper geometry as well as structural items
  defined by the components described before can be used. An additional component, called SOFiLOAD allows to convert these loading
  items into SOFiLOAD input.

Following image displayes an example together with the generated SOFiSTiK analytical model:

![Creation of Girder System](https://github.com/SOFiSTiK/gh_sofistik/blob/master/gh_sofistik/examples/img/girder_system_01.JPG)

The components for defining structural elements in the structural group also support 'Baking' in a way that the defined 
analytical properties can be transferred to Rhino being readable by the SOFiSTiK Rhino Interface there.
This means, the geometry baked within Rhino can be further edited and meshed to a SOFiSTiK analytical model from there - 
provided, of course, you have the SOFiSTiK Rhino Interface installed.
See the e.g. the [Online Documentation of the Rhino Interface](https://www.sofistik.de/documentation/2018/en/rhino_interface/index.html) for further information. 

Please note: the grasshopper assembly is continuously being developed and at this stage we cannot guarantee that, e.g.
node and parameter names may not change. However these changes mainly take place in the master branch.
For productive usage, we recommend to switch to a stable branch, available in parallel to the master, and to download the 
gha assembly from there. Stable branches may receive fixes but will not undergo larger changes.