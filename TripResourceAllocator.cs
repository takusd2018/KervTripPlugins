using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace KervTripPlugins
{
    public class TripResourceAllocator : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {// Obtain the tracing service
         // Extract the tracing service for use in debugging sandboxed plug-ins.  
         // If you are not registering the plug-in in the sandbox, then you do  
         // not have to add any tracing service related code.  
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));             // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
             serviceProvider.GetService(typeof(IPluginExecutionContext));             // Obtain the organization service reference which you will need for  
            // web service calls.  
            IOrganizationServiceFactory serviceFactory =
            (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);              // The InputParameters collection contains all the data passed in the message request.  
            if (context.InputParameters.Contains("Target") &&
            context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parameters.  
                Entity tripEntity = (Entity)context.InputParameters["Target"];
                try
                {
                    // Plug-in business logic goes here.                     
                    DateTime startDate = tripEntity.GetAttributeValue<DateTime>("new_startedate");

                    DateTime endDate = tripEntity.GetAttributeValue<DateTime>("new_enddate");
                    int? headCount= tripEntity.GetAttributeValue<int>("new_headcount");
                 
                    if (startDate==null || endDate == null ||  headCount == null) return;
                    //Create a query expression for reading trip resources
                    QueryExpression qeTripResources = new QueryExpression("new_tripresource");

                    //add the columns 
                    qeTripResources.ColumnSet.AddColumns("new_availablefrom", "new_availableto","new_amount","new_trip", "new_headcount", "new_tripresourceid");


                    FilterExpression filter = new FilterExpression(LogicalOperator.And);
                    filter.AddCondition("new_availablefrom", ConditionOperator.LessEqual, startDate);
                    filter.AddCondition("new_availableto",ConditionOperator.GreaterEqual, endDate);
                    filter.AddCondition("new_headcount",ConditionOperator.GreaterEqual,headCount);

                    qeTripResources.Criteria.AddFilter(filter);


                    EntityCollection resourcesCollection = service.RetrieveMultiple(qeTripResources);

                    if (resourcesCollection == null) return;
                    int totalResourceCount = resourcesCollection.Entities.Count;

                    

                    tripEntity["new_amount"] = new Money(0);
                    service.Update(tripEntity);
                    if (totalResourceCount> 0)
                    {
                        foreach(var resource in resourcesCollection.Entities)
                        {
                            var resourceHeadCount=resource.GetAttributeValue<int>("new_headcount");
                            var resourceAmount=resource.GetAttributeValue<Money>("new_amount");
                            var totalAmount = resourceAmount.Value * resourceHeadCount;

                            tripEntity["new_amount"] =new Money(tripEntity.GetAttributeValue<Money>("new_amount").Value + totalAmount);
                            service.Update(tripEntity);

                            resource["new_trip"] = new EntityReference("new_trip",tripEntity.Id);
                            service.Update(resource);

                        }
                    }
                     
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in MyPlug-in.", ex);
                }
                catch (Exception ex)
                {
                    tracingService.Trace("MyPlugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}
