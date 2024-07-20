using api;
using MongoDB.Driver;
using processing.Models;

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

        public List<Log> GetAsync() =>
            _logCollection.Find(_ => true).ToList();

        public Log? GetAsync(string id) =>
            _logCollection.Find(x => x.fileId == id).FirstOrDefault();

        public Log? FindByFileName(string fileName) =>
            _logCollection.Find(x => x.fileName == fileName).FirstOrDefault();

        public void CreateAsync(Log newBook) =>
            _logCollection.InsertOne(newBook);

        public void UpdateAsync(string id, Log updatedLog)
        {
            // var filter = Builders<Log>.Filter
            //             .Eq(x => x.fileName, id);
            // var update = Builders<Log>.Update
            // .Set(x => x.status, updatedBook.status)
            // .Set(x => x.NoOfBatchesCreated, updatedBook.NoOfBatchesCreated)
            // .Set(x => x.BatchData, updatedBook.BatchData)
            // .Set(x => x.totalNumberOfBatchesCreated, updatedBook.totalNumberOfBatchesCreated);
            // _logCollection.UpdateOne(filter, update);
            var filter = Builders<Log>.Filter.Eq(log => log.fileId, id);
            _logCollection.ReplaceOne(filter, updatedLog);
        }

        public async Task RemoveAsync(string id) =>
            await _logCollection.DeleteOneAsync(x => x.fileId == id);
    }
}