using k8s.Models;

var builder = DistributedApplication.CreateBuilder(args);

var cloudNode01 = builder.AddProject<Projects.Presentation_Edge_Node>("cloud-node-01");

builder.AddProject<Projects.Presentation_Edge_Node>("edge-node-01")
    .WaitFor(cloudNode01);

builder.AddProject<Projects.Presentation_Edge_Node>("edge-node-02")
    .WaitFor(cloudNode01);

builder.AddProject<Projects.Presentation_Edge_Node>("edge-node-03")
    .WaitFor(cloudNode01);

builder.Build().Run();
