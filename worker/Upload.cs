using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using worker.Models;
using MySqlConnector;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Polly;
using processing.Service;
using Polly.Retry;
using api;
using worker.Service;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using System.IO.Compression;

namespace worker
{
    public class Upload
    {
        ConnectionFactory factory;
        private readonly SaveLog _savelog;
        private readonly LogService _logService;

        public Upload(SaveLog saveLog, LogService logService)
        {
            factory = new ConnectionFactory { HostName = "localhost" };
            _savelog = saveLog;
            _logService = logService ?? throw new ArgumentNullException(nameof(logService)); ;
        }

        public static string CompressString(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length);
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
            return Convert.ToBase64String(gZipBuffer);
        }
        // public static string DecompressString(string compressedText)
        // {
        //     // string converted = compressedText.Replace('-', '+');
        //     // converted = converted.Replace('_', '/');
        //     byte[] gZipBuffer = Encoding.UTF8.GetBytes(compressedText);;
        //     using (var memoryStream = new MemoryStream())
        //     {
        //         int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
        //         memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

        //         var buffer = new byte[dataLength];

        //         memoryStream.Position = 0;
        //         using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
        //         {
        //             gZipStream.Read(buffer, 0, buffer.Length);
        //         }

        //         return Encoding.UTF8.GetString(buffer);
        //     }
        // }
        public static string DecompressString(string compressedText)
        {
            byte[] gZipBuffer = Convert.FromBase64String(compressedText);

            using (var memoryStream = new MemoryStream(gZipBuffer))
            {
                // Read the first 4 bytes to get the original buffer length
                var bufferLengthBytes = new byte[4];
                memoryStream.Read(bufferLengthBytes, 0, 4);
                int bufferLength = BitConverter.ToInt32(bufferLengthBytes, 0);

                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    byte[] buffer = new byte[bufferLength];
                    gZipStream.Read(buffer, 0, buffer.Length);

                    return Encoding.UTF8.GetString(buffer);
                }
            }
        }

        public void Start()
        {
            using var RabbitMQconnection = factory.CreateConnection();
            using var channel = RabbitMQconnection.CreateModel();
            channel.QueueDeclare(queue: "database_queue",
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
            // Console.WriteLine(" [*] Waiting for messages.");

            List<BatchUpload> Batches = new List<BatchUpload>();
            List<string> exhaustedCommands = new List<string>();

            var _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(8, retryAttempt =>
                {
                    var timeToWait = TimeSpan.FromSeconds(7);
                    Console.WriteLine($"Waiting {timeToWait.TotalSeconds} seconds");
                    return timeToWait;
                });
            // var fallbackPolicy = Policy
            // .Handle<Exception>()
            // .FallbackAsync(async (context, ct) => {
            //     if (context.ContainsKey("LastCommand"))
            //     {
            //         string fallbackData = context["LastCommand"] as string;
            //         exhaustedCommands.Add(fallbackData);
            //         // Console.WriteLine($"Added to exhausted commands: {fallbackData}");
            //     }
            // }, async (exception, context) => {
            //     // Console.WriteLine($"Exception for fallback: {exception.Message}");
            // });

            // var _policyWrap = Policy.WrapAsync( fallbackPolicy,_retryPolicy);
            // var batch = 0;
            var consumer = new EventingBasicConsumer(channel);
            BatchUpload Batch = new BatchUpload();
            consumer.Received += async (model, ea) =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var body = ea.Body.ToArray();
                var m = Encoding.UTF8.GetString(body);
                var cmd = "";
                dynamic? sm = System.Text.Json.JsonSerializer.Deserialize<SendModel>(m)!;
                cmd = Encoding.UTF8.GetString(sm.Command);
                Batch = sm.Batch;
                // Console.WriteLine(Encoding.UTF8.GetString(sm.bacthNo));
                // int batch = Convert.ToInt32(Encoding.UTF8.GetString(sm.bacthNo));
                // var b = sm.bacthNo;
                // // Console.WriteLine(b);
                // Console.WriteLine(cmd);
                // Console.WriteLine(Batch.BatchNumber);
                // int batch = BitConverter.ToInt32(b,0);
                cmd = cmd.Remove(cmd.Length - 1);
                cmd += " ON DUPLICATE KEY UPDATE NAME=VALUES(NAME),COUNTRY=VALUES(COUNTRY),STATE=VALUES(STATE),CITY=VALUES(CITY),TELEPHONE=VALUES(TELEPHONE),AddressLine1=VALUES(AddressLine1),AddressLine2=VALUES(AddressLine2),DateOfBirth=VALUES(DateOfBirth),FY_2019_20=VALUES(FY_2019_20),FY_2020_21=VALUES(FY_2020_21),FY_2021_22=VALUES(FY_2021_22);";
                // sm.log.BatchData[sm.log.BatchData.Count-1].command = sm.log.BatchData[sm.log.BatchData.Count-1].command.Remove(sm.log.BatchData[sm.log.BatchData.Count-1].command.Length-1);
                // sm.log.BatchData[sm.log.BatchData.Count-1].command += " ON DUPLICATE KEY UPDATE NAME=VALUES(NAME),COUNTRY=VALUES(COUNTRY),STATE=VALUES(STATE),CITY=VALUES(CITY),TELEPHONE=VALUES(TELEPHONE),AddressLine1=VALUES(AddressLine1),AddressLine2=VALUES(AddressLine2),DateOfBirth=VALUES(DateOfBirth),FY_2019_20=VALUES(FY_2019_20),FY_2020_21=VALUES(FY_2020_21),FY_2021_22=VALUES(FY_2021_22);";
                var contextData = new Context();
                contextData["CurrentCommand"] = cmd;
                // await _retryPolicy.ExecuteAsync(async () =>
                // {
                using (var connection = new MySqlConnection("Server=127.0.0.1;port=3333;User=root;Password=root;Database=csv;AllowLoadLocalInfile=true;Allow User Variables=true;Connection TImeout=150;MaxPoolSize=20"))
                {
                    connection.Open();
                    using (MySqlCommand myCmd = new MySqlCommand(cmd, connection))
                    {
                        myCmd.CommandType = CommandType.Text;
                        int executedOrNot = await myCmd.ExecuteNonQueryAsync();
                        try
                        {
                            if (executedOrNot > 0)
                            {
                                // var log = await _logService.GetLogByBatchNumberAsync(sm.log.BatchData[sm.log.BatchData.Count-1].BatchNumber);
                                // Console.WriteLine(log.fileId);
                                // await _logService.UpdateBatchUploadAsync(log.fileId,b,sm.log.BatchData[sm.log.BatchData.Count-1].BatchNumber);
                                // logData(b,log,exhaustedCommands);
                                logData(Batch);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }

                    }
                    connection.Close();
                }
                // });

                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                Console.WriteLine($"Time takes: {elapsedMs / 1000}s");
                // });

            };
            Console.WriteLine("Uploaded");


            channel.BasicConsume(queue: "database_queue",
                                autoAck: true,
                                consumer: consumer);

            Console.ReadLine();
        }
        private async void logData(BatchUpload b)
        {
            // Console.WriteLine("Data Logging Started");
            Log? log = _logService.GetAsync(b.fileId);
            if (log != null)
            {
                // Console.WriteLine("Inside if");
                log.BatchData.Add(b);
                log.NoOfBatchesCreated = log.BatchData.Count;
                if (log.status == "Processed") log.status = "Uploading...";
                if (log.totalNumberOfBatchesCreated == log.NoOfBatchesCreated) log.status = "Uploaded";
                try
                {
                    await _logService.UpdateAsync(log.fileId, log);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            Console.WriteLine("Data Logged");
        }

        // private async void logData(Log log)
        // {
        //     // Console.WriteLine("Data Logging Started");
        //     Log? f = _logService.GetAsync(log.fileId);
        //     if (f != null)
        //     {
        //         // Console.WriteLine("Inside if");
        //         log.NoOfBatchesCreated = log.BatchData.Count;
        //         if (log.status == "Processed") log.status = "Uploading...";
        //         // if (log.BatchData[log.BatchData.Count - 1].isUploaded == true) log.status = "Uploaded";
        //         try
        //         {
        //             await _logService.UpdateAsync(log.fileId, log);
        //         }
        //         catch (Exception e)
        //         {
        //             Console.WriteLine(e.Message);
        //         }
        //     }
        //     Console.WriteLine("Data Logged");
        // }

    }
}


// if(f == null){
// Console.WriteLine("Inside if");
//                 log.NoOfBatchesCreated = batches.Count;

//                 log.BatchData = batches;
//                 if(totalNumberOfBatchesCreated== log.NoOfBatchesCreated) {
// Console.WriteLine("Inside");
//                     log.status = "Uploaded";
//                 }
//                 // log.NotUploaded = NotUploadedBatches;
//                 _logService.CreateAsync(log);
//             }else{
// Console.WriteLine("Inside else");

//                 Log temp = new Log{
//                     fileId = f.fileId,
//                     fileName = f.fileName,
//                     status = f.status,
//                     totalNumberOfBatchesCreated = f.totalNumberOfBatchesCreated,
//                     NoOfBatchesCreated = batches.Count,
//                     BatchData = batches
//                 };

//                 if(temp.totalNumberOfBatchesCreated == temp.NoOfBatchesCreated) {
// Console.WriteLine("Inside");
//                     temp.status = "Uploaded";
//                 }
//                 // temp.NotUploaded = NotUploadedBatches;
//                 _logService.UpdateAsync(log.fileName,temp);
//             }