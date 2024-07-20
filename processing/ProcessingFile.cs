using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net.Mail;
using System.Text;
// using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Newtonsoft.Json;
using processing.Models;
using processing.Service;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using worker.Service;
namespace processing
{
    public class ProcessingFile
    {
        ConnectionFactory factory;
        private readonly SaveLog _savelog;
        private readonly LogService _logService;

        public ProcessingFile(SaveLog saveLog, LogService logService)
        {
            factory = new ConnectionFactory { HostName = "localhost" };
            _savelog = saveLog;
            _logService = logService ?? throw new ArgumentNullException(nameof(logService)); ;
        }


        public void Start()
        {
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            channel.QueueDeclare(queue: "process_queue",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            Console.WriteLine(" [*] Waiting for file to Come");

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                Console.WriteLine("File Processing");
                var fileBytes = ea.Body.ToArray();
                var m = Encoding.UTF8.GetString(fileBytes);
                RecieveModel rm = System.Text.Json.JsonSerializer.Deserialize<RecieveModel>(m)!;
                using MemoryStream memoryStream = new MemoryStream(rm.fileBytes);
                using StreamReader reader = new StreamReader(memoryStream, Encoding.UTF8);
                await ParseFile(reader, rm.log);
            };
            channel.BasicConsume(queue: "process_queue",
                    autoAck: true,
                    consumer: consumer);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
        public static string CompressString(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);

            using (var memoryStream = new MemoryStream())
            {
                // Write the buffer length as the first 4 bytes
                memoryStream.Write(BitConverter.GetBytes(buffer.Length), 0, 4);

                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    gZipStream.Write(buffer, 0, buffer.Length);
                }

                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        private async Task<bool> ParseFile(StreamReader reader, Log log)
        {
            StringBuilder command = new StringBuilder("INSERT INTO Users (Name,EmaiL,Country,State,City,Telephone,AddressLine1,AddressLine2,DateOfBirth,FY_2019_20,FY_2020_21,FY_2021_22) VALUES");
            string startCmd = "INSERT INTO Users (Name,EmaiL,Country,State,City,Telephone,AddressLine1,AddressLine2,DateOfBirth,FY_2019_20,FY_2020_21,FY_2021_22) VALUES";

            const int batchSize = 100;
            var added = 0;
            var batches = 0;
            while (reader.Peek() >= 0)
            {
                // Console.WriteLine(reader.Peek());
                var result = await reader.ReadLineAsync();
                // Console.WriteLine(result+" ");
                var a = result?.Split(",");
                bool hasNullOrEmpty = a!.Any(string.IsNullOrEmpty);

                if (!hasNullOrEmpty && result?.Length > 0)
                {
                    // var a = result?.Split(",");
                    string add = $" ('{MySqlHelper.EscapeString(a![0])}','{MySqlHelper.EscapeString(a[1])}', '{MySqlHelper.EscapeString(a[2])}','{MySqlHelper.EscapeString(a[3])}','{MySqlHelper.EscapeString(a[4])}','{MySqlHelper.EscapeString(a[5])}','{MySqlHelper.EscapeString(a[6])}','{MySqlHelper.EscapeString(a[7])}','{DateTime.Parse(a[8]).ToString("yyyy-MM-dd")}','{a[9]}','{a[10]}','{a[11]}'),";
                    // string salaryAdd = $" ('{start}','{a[9]}','{a[10]}','{a[11]}','{start}'),";
                    command.Append(add);
                    // SalaryCommand.Append(salaryAdd);
                    added++;
                }
                if (added == batchSize || reader.Peek() <= 0)
                {
                    // Console.WriteLine($"batch {added}");
                    added = 0;
                    string cmd = command.ToString();
                    // string salaryCmd = SalaryCommand.ToString();
                    // string  send = cmd+':'+salaryCmd;
                    command = new StringBuilder("INSERT INTO users (Name,EmaiL,Country,State,City,Telephone,AddressLine1,AddressLine2,DateOfBirth,FY_2019_20,FY_2020_21,FY_2021_22) VALUES");
                    // SalaryCommand = new StringBuilder("INSERT IGNORE INTO salary (SalaryId,FY_2019_20,FY_2020_21,FY_2021_22,UserId) VALUES");
                    // Console.WriteLine($"Batch {batches++} send");
                    batches++;
                    BatchUpload b = new BatchUpload
                    {
                        BatchNumber = batches,
                        fileId = log.fileId,
                        command = cmd
                    };
                    // log.BatchData.Add(new BatchUpload
                    // {
                    //     isUploaded = false,
                    //     BatchNumber = batches,
                    // });
                    // if(cmd != startCmd){
                    if (reader.Peek() <= 0)
                    {

                        log.status = "Processed";
                        log.totalNumberOfBatchesCreated = batches;
                        _logService.UpdateAsync(log.fileId, log);
                        Console.WriteLine("Data Processing Log Updated");
                    }
                    DatabaseSend(cmd, b);
                    // }else{
                    //     Console.WriteLine("Nothing To send");
                    // }
                }
            }

            Console.WriteLine("Complete Data Proceesed");
            return true;
        }
        private void DatabaseSend(string command, BatchUpload b)
        {
            // Console.WriteLine("data send");
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            channel.QueueDeclare(queue: "database_queue",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            var cmd = Encoding.UTF8.GetBytes(command);


            // var number = Encoding.UTF8.GetBytes(Convert.ToString(bacthes));
            SendModel sdm = new SendModel
            {
                Command = cmd,
                Batch = b
            };
            var message = JsonConvert.SerializeObject(sdm);
            var obj = Encoding.UTF8.GetBytes(message);
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            channel.BasicPublish(exchange: string.Empty,
                                routingKey: "database_queue",
                                body: obj);

        }
        // private void DatabaseSend(string send, Log log, int batches)
        // {
        //     // Console.WriteLine("data send");
        //     var connection = factory.CreateConnection();
        //     var channel = connection.CreateModel();
        //     channel.QueueDeclare(queue: "database_queue",
        //         durable: false,
        //         exclusive: false,
        //         autoDelete: false,
        //         arguments: null);
        //     var body = Encoding.UTF8.GetBytes(send);
        //     // string n = Convert.ToBase64String(BitConverter.GetBytes(batches));
        //     // var number  = Encoding.UTF8.GetBytes(n);
        //     Console.WriteLine(batches);
        //     byte[] number = BitConverter.GetBytes(batches);
        //     if (BitConverter.IsLittleEndian)
        //     {
        //         Array.Reverse(number);
        //     }

        //     // var number = Encoding.UTF8.GetBytes(Convert.ToString(bacthes));
        //     SendModel sdm = new SendModel
        //     {
        //         fileBytes = body,
        //         log = log,
        //         bacthNo = number
        //     };
        //     var message = JsonConvert.SerializeObject(sdm);
        //     var obj = Encoding.UTF8.GetBytes(message);
        //     channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        //     channel.BasicPublish(exchange: string.Empty,
        //                         routingKey: "database_queue",
        //                         body: obj);

        // }

    }

}