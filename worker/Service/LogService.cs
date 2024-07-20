using api;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using worker.Models;

namespace worker.Service
{
    public class LogService
    {
        private readonly IMongoCollection<Log> _logCollection;

    public LogService(LogDatabaseSetting bookStoreDatabaseSettings)
    {
        var mongoClient = new MongoClient(
            bookStoreDatabaseSettings.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            bookStoreDatabaseSettings.DatabaseName);

        _logCollection = mongoDatabase.GetCollection<Log>(
            bookStoreDatabaseSettings.LogCollectionName);
    }

    public  List<Log> GetAsync() =>
        _logCollection.Find(_ => true).ToList();

    public Log? GetAsync(string id) =>
        _logCollection.Find(x => x.fileId == id).FirstOrDefault();

    public Log? FindByFileName(string fileName) =>
        _logCollection.Find(x => x.fileName == fileName).FirstOrDefault();

    public  void CreateAsync(Log newBook) =>
        _logCollection.InsertOne(newBook);

    public async Task<Log> GetLogByBatchNumberAsync(int batchNumber)
    {
        var filter = Builders<Log>.Filter.ElemMatch(log => log.BatchData, batch => batch.BatchNumber == batchNumber);
        return await _logCollection.Find(filter).FirstOrDefaultAsync();
    }
    public async Task UpdateAsync(string id, Log updatedLog) {
        // var filter = Builders<Log>.Filter
        //             .Eq(x => x.fileName, id);
        // var update = Builders<Log>.Update
        // .Set(x => x.status, updatedBook.status)
        // .Set(x => x.NoOfBatchesCreated, updatedBook.NoOfBatchesCreated)
        // .Set(x => x.BatchData, updatedBook.BatchData);
        // _logCollection.UpdateOne(filter, update);
        var filter = Builders<Log>.Filter.Eq(log => log.fileId, id);
            await _logCollection.ReplaceOneAsync(filter, updatedLog);
    }
    // public async Task UpdateBatchUploadAsync(string logId, BatchUpload updatedBatch, int batchNumber)
    // {
    //     var filter = Builders<Log>.Filter.And(
    //         Builders<Log>.Filter.Eq(log => log.fileId, logId),
    //         Builders<Log>.Filter.ElemMatch(log => log.BatchData, batch => batch.BatchNumber == updatedBatch.BatchNumber)
    //     );
    //     var update = Builders<Log>.Update
    //         .Set(log => log.BatchData[batchNumber].isUploaded, updatedBatch.isUploaded)
    //         .Set(log => log.BatchData[batchNumber].command, updatedBatch.command);

    //     await _logCollection.UpdateOneAsync(filter, update);
    // }
    public async Task UpdateBatchUploadAsync(string logId, BatchUpload updatedBatch,int BatchNumber)
    {
        // Step 1: Retrieve the Log document
        var log = await _logCollection.Find(log => log.fileId == logId).FirstOrDefaultAsync();

        if (log != null)
        {
            // Step 2: Find the index of the BatchUpload to be updated
            var index = log.BatchData.FindIndex(b => b.BatchNumber == BatchNumber);

            if (index != -1)
            {
                // Step 3: Update the specific BatchUpload element
                log.BatchData[index] = updatedBatch;

                // Step 4: Replace the entire Log document
                await _logCollection.ReplaceOneAsync(log => log.fileId == logId, log);
            }
            else
            {
                Console.WriteLine("Batch not found.");
            }
        }
        else
        {
            Console.WriteLine("Log not found.");
        }
    }

    public async Task RemoveAsync(string id) =>
        await _logCollection.DeleteOneAsync(x => x.fileId == id);
    }
    
}