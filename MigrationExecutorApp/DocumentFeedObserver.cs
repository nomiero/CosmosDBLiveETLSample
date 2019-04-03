namespace MigrationConsoleApp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.CosmosDB.BulkExecutor;
    using Microsoft.Azure.CosmosDB.BulkExecutor.BulkImport;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Documents.Client;
    using ChangeFeedObserverCloseReason = Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing.ChangeFeedObserverCloseReason;
    using IChangeFeedObserver = Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing.IChangeFeedObserver;

    public class DocumentFeedObserver: IChangeFeedObserver
    {
        private readonly DocumentClient client;
        private readonly Uri destinationCollectionUri;
        private IBulkExecutor bulkExecutor;
        private IDocumentTransformer documentTransformer;

        public DocumentFeedObserver(DocumentClient client, DocumentCollectionInfo destCollInfo, IDocumentTransformer documentTransformer)
        {
            this.client = client;
            this.destinationCollectionUri = UriFactory.CreateDocumentCollectionUri(destCollInfo.DatabaseName, destCollInfo.CollectionName);
            this.documentTransformer = documentTransformer;
        }

        public async Task OpenAsync(IChangeFeedObserverContext context)
        {
            client.ConnectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 100000;
            client.ConnectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 100000;

            DocumentCollection destinationCollection = await client.ReadDocumentCollectionAsync(this.destinationCollectionUri);
            bulkExecutor = new BulkExecutor(client, destinationCollection);
            client.ConnectionPolicy.UserAgentSuffix = (" migrationService");

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Observer opened for partition Key Range: {0}", context.PartitionKeyRangeId);
            Console.ForegroundColor = ConsoleColor.White;

            await bulkExecutor.InitializeAsync();
        }

        public Task CloseAsync(IChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Observer closed, {0}", context.PartitionKeyRangeId);
            Console.WriteLine("Reason for shutdown, {0}", reason);
            Console.ForegroundColor = ConsoleColor.White;

            return Task.CompletedTask;
        }

        public async Task ProcessChangesAsync(
            IChangeFeedObserverContext context, 
            IReadOnlyList<Document> docs, 
            CancellationToken cancellationToken)
        {
            try
            {
                List<Document> transformedDocs = new List<Document>();
                foreach(var doc in docs)
                {
                    transformedDocs.AddRange(documentTransformer.TransformDocument(doc).Result);
                }

                BulkImportResponse bulkImportResponse = await bulkExecutor.BulkImportAsync(
                    documents: transformedDocs,
                    enableUpsert: true,
                    maxConcurrencyPerPartitionKeyRange: 1,
                    disableAutomaticIdGeneration: true,
                    maxInMemorySortingBatchSize: null,
                    cancellationToken: new CancellationToken());

                LogMetrics(context, bulkImportResponse);
            }
            catch (Exception e)
            {
                Program.telemetryClient.TrackException(e);
            }

            Program.telemetryClient.Flush();
        }

        private static void LogMetrics(IChangeFeedObserverContext context, BulkImportResponse bulkImportResponse)
        {
            Console.WriteLine("Imported Documents: " + bulkImportResponse.NumberOfDocumentsImported
                                + "  by process " + Process.GetCurrentProcess().Id);
            Console.WriteLine("RUs consumed : " + bulkImportResponse.NumberOfDocumentsImported
                + " by process " + Process.GetCurrentProcess().Id);

            Program.telemetryClient.TrackMetric("TotalInserted", bulkImportResponse.NumberOfDocumentsImported);

            Program.telemetryClient.TrackMetric("InsertedDocuments-Process:"
                + Process.GetCurrentProcess().Id, bulkImportResponse.NumberOfDocumentsImported);
            Program.telemetryClient.TrackMetric("TotalRUs", bulkImportResponse.TotalRequestUnitsConsumed);
        }
    }
}
