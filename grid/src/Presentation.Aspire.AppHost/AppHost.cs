var builder = DistributedApplication.CreateBuilder(args);

var router = builder.AddProject<Projects.Presentation_Cloud_Router>("router");

var cloudNode01 = builder.AddProject<Projects.Presentation_Cloud_Node>("cloud-node-01")
    .WaitFor(router);

builder.AddProject<Projects.Presentation_Edge_Node>("edge-node-01")
    .WaitFor(cloudNode01);

builder.AddProject<Projects.Presentation_Edge_Node>("edge-node-02")
    .WaitFor(cloudNode01);

builder.AddProject<Projects.Presentation_Edge_Node>("edge-node-03")
    .WaitFor(cloudNode01);

builder.Build().Run();
