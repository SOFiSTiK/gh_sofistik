This example shows how to create a C# custom component in Grasshopper allowing to bake Rhino geometry with SOFiSTiK structural properties assigned.

When activating the custom bake command, the Grasshopper surface geometry provided on input will be baked to a Rhino object
which is already marked as a SOFiSTiK structural surface which can be meshed and modified using the SOFiSTiK Rhino Interface.

Exemplary the group number and thickness has been set within Grasshopper.
Other parameters can be set accordingly as key value pair a Rhino user string.
For other available keys check out the Rhino command _GetUserString for a already assigned structural object.


