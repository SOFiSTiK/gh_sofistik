This example shows how to create a C# custom scripting component in Grasshopper allowing to bake Rhino geometry with SOFiSTiK structural properties assigned.

When triggering the custom bake command, the Grasshopper surface geometry provided on input will be baked into a Rhino object
which is already marked as a SOFiSTiK structural surface and which can be meshed and modified using the SOFiSTiK Rhino Interface.

Exemplary the group number and thickness has been set within Grasshopper.
Other parameters can be added accordingly by setting the respective key value pair as Rhino User String.
Please check out the command "_GetUserString" in Rhino to get the keys of the other parameters.


