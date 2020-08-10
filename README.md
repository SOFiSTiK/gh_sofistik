# gh_sofistik
Grasshopper components allowing to add analytical properties to Grasshopper geometry and to convert this information to SOFiSTiK input files

Install the components by dragging & dropping the assembly gh_sofistik.gha from the subfolder ./bin onto the Grasshopper window. After installation a SOFiSTiK tab should appear in the control panel of Grasshopper containing four groups of components:

* General: provides a component to start an analysis calculations directly from grasshopper, a component to stream text data to *.dat files or
  a component to adjust visualization settings globally.
* Section: provides components to create & attribute SOFiSTiK sections from planar geometry, to inspect the sections and to write this
  information to SOFiSTiK files.
* Structure: provides components to convert points, lines and areas to SOFiSTiK structural elements as well as components to
  create rigid and elastic couplings between these structural elements.
  All these structural elements can then be passed to a further component, called SOFiMSHC which converts the structural
  information into a SOFiSTiK input which can be streamed to a file and calculated there.
* Loads: provides components to add loads to points, lines and areas. As input, Grasshopper geometry as well as structural items
  defined by the components described before can be used. An additional component, called SOFiLOAD allows to convert these loading
  items into SOFiLOAD input.

Detailed information of the components can be found in the official documentation of the SOFiSTiK Grasshopper components: [www.sofistik.de/documentation/2020/en/rhino_interface/](https://www.sofistik.de/documentation/2020/en/rhino_interface/grasshopper/command_reference.html)

The components for defining structural elements in the structural group also support 'Baking' in a way that the defined 
analytical properties can be transferred to Rhino being readable by the SOFiSTiK Rhino Interface there.
This means, the geometry baked within Rhino can be further edited and meshed to a SOFiSTiK analytical model from there - 
provided, of course, you have the SOFiSTiK Rhino Interface installed.


## Commerical version

Further components with additional functionality, e.g. allowing to define parametric bridge models with varying cross sections or to define pre- and posttensioning tendons 
will be available in a commercial version. For further information please check the following website: [www.sofistik.com/products/finite-elements/rhinoceros-interface](https://www.sofistik.com/products/finite-elements/rhinoceros-interface)


## Example

Examples can be found in the subfolder [gh_sofistik/examples](https://github.com/SOFiSTiK/gh_sofistik/tree/master/gh_sofistik/examples):

![Creation of Girder System](https://github.com/SOFiSTiK/gh_sofistik/blob/master/gh_sofistik/examples/img/girder_system_01.JPG)


## Please Note

Please note: the grasshopper assembly is continuously being developed and at this stage we cannot guarantee that, e.g.
node and parameter names may not change. However these changes mainly take place in the master branch.
For productive usage, we recommend to switch to a stable branch, available in parallel to the master, and to download the 
gha assembly from there. Stable branches may receive fixes but will not undergo larger changes.