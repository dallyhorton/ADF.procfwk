using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Management.DataFactory;
using Microsoft.Azure.Management.DataFactory.Models;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PipelineExecutor
{ 
    public static class ExecutePipelineV2
    {
        [FunctionName("ExecutePipelineV2")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("ExecutePipelineV2 Function triggered by HTTP request.");
            log.LogInformation("Parsing body from request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string tenantId = data?.tenantId;
            string applicationId = data?.applicationId;
            string authenticationKey = data?.authenticationKey;
            string subscriptionId = data?.subscriptionId;
            string resourceGroup = data?.resourceGroup;
            string factoryName = data?.factoryName;
            string pipelineName = data?.pipelineName;

            //Check body for values
            if (
                tenantId == null ||
                applicationId == null ||
                authenticationKey == null ||
                subscriptionId == null ||
                resourceGroup == null ||
                factoryName == null ||
                pipelineName == null
                )
            {
                log.LogInformation("Invalid body.");
                return new BadRequestObjectResult("Invalid request body, value missing.");
            }

            //Create a data factory management client
            log.LogInformation("Creating ADF connectivity client.");
            string outputString = string.Empty;

            using (var client = Helpers.DataFactoryClient.createDataFactoryClient(tenantId, applicationId, authenticationKey, subscriptionId))
            {
                //Run pipeline
                CreateRunResponse runResponse;
                PipelineRun pipelineRun;

                if (data?.pipelineParameters == null)
                {
                    log.LogInformation("Called pipeline without parameters.");

                    runResponse = client.Pipelines.CreateRunWithHttpMessagesAsync(
                        resourceGroup, factoryName, pipelineName).Result.Body;
                }
                else
                {
                    log.LogInformation("Called pipeline with parameters.");

                    JObject jObj = JObject.Parse(requestBody);
                    Dictionary<string, object> parameters = jObj["pipelineParameters"].ToObject<Dictionary<string, object>>();

                    log.LogInformation("Number of parameters provided: " + jObj.Count.ToString());

                    runResponse = client.Pipelines.CreateRunWithHttpMessagesAsync(
                        resourceGroup, factoryName, pipelineName, parameters: parameters).Result.Body;
                }

                log.LogInformation("Pipeline run ID: " + runResponse.RunId);

                //Wait and check for pipeline to start...
                log.LogInformation("Checking ADF pipeline status.");
                while (true)
                {
                    pipelineRun = client.PipelineRuns.Get(
                        resourceGroup, factoryName, runResponse.RunId);

                    log.LogInformation("ADF pipeline status: " + pipelineRun.Status);

                    if (pipelineRun.Status == "Queued")
                        System.Threading.Thread.Sleep(15000);
                    else
                        break;
                }

                //Final return detail
                outputString = "{ \"PipelineName\": \"" + pipelineName +
                                        "\", \"RunId\": \"" + pipelineRun.RunId +
                                        "\", \"Status\": \"" + pipelineRun.Status +
                                        "\" }";

                
            }

            JObject outputJson = JObject.Parse(outputString);

            log.LogInformation("ExecutePipelineV2 Function complete.");
            return new OkObjectResult(outputJson);
        }
    }
}
