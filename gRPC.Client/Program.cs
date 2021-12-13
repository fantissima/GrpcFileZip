using gRPC.Server_v4;
using Grpc.Net.Client;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using System.Threading;
using System.Collections.Generic;

namespace gRPC.Client
{
    class Program
    {

        static async Task Main(string[] args)
        {
            Thread.Sleep(5000);
            GrpcChannel channel = GrpcChannel.ForAddress("https://localhost:5001");
            FileTransfer.FileTransferClient fileTransferClient = new FileTransfer.FileTransferClient(channel);
           
            string filePath = @"C:\Users\kfranke\Documents\";
            string fileName = @"OMNIA_2021_10_01_16_15_22_neu.txt";
            string file = filePath + fileName;

            byte[] fileContent = File.ReadAllBytes(file);

            //get connection to FileTransferService
            using (AsyncDuplexStreamingCall<FileUploadRequest, FileUploadReply> asyncDuplexStreamingCall = fileTransferClient.Upload())
            {
                await StreamFileToServer(file, fileContent, asyncDuplexStreamingCall);
                await GetFileFromServer(asyncDuplexStreamingCall, filePath, fileName);
            }
        }


        private static async Task StreamFileToServer(String file, byte[] fileContent, AsyncDuplexStreamingCall<FileUploadRequest, FileUploadReply> asyncDuplexStreamingCall)
        {

            int maxTransferSize = 2 << 20;
            int fileParts = fileContent.Length / maxTransferSize;
            int fileLeftOver = fileContent.Length % maxTransferSize;

            if (fileLeftOver != 0)
                fileParts++;


            FileUploadRequest request = new FileUploadRequest() { FileSize = fileContent.Length, FilePath = file };
            byte[] sendContent = new byte[maxTransferSize];

            for (int index = 0; index < fileParts; index++)
            {
                int startPos = index * maxTransferSize;
                // transfer leftover part
                if (index == (fileParts - 1))
                {
                    sendContent = new byte[fileLeftOver];
                    Array.Copy(fileContent, startPos, sendContent, 0, fileLeftOver);
                }
                else
                {
                    Array.Copy(fileContent, startPos, sendContent, 0, maxTransferSize);
                }

                request.FileContent = Google.Protobuf.ByteString.CopyFrom(sendContent);
                await asyncDuplexStreamingCall.RequestStream.WriteAsync(request);
            }
        }


        private static async Task GetFileFromServer(AsyncDuplexStreamingCall<FileUploadRequest, FileUploadReply> asyncDuplexStreamingCall, string filePath, string fileName)
        {
            List<byte> content = new List<byte>();

            await foreach (var response in asyncDuplexStreamingCall.ResponseStream.ReadAllAsync())
            {
                content.AddRange(response.FileContent.ToByteArray());
                if (content.Count == response.FileSize)
                {
                    File.WriteAllBytes(filePath + Path.GetFileNameWithoutExtension(fileName) + ".zip", content.ToArray());
                }
            }
        }
    }
}