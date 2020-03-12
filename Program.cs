using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using System.Threading;
using System.IO;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace KeyVaultClientEncription
{
    class Program
    {
        static void Main(string[] args)
        {
            RunAsyncProgram().GetAwaiter().GetResult();

        }
        private static async Task RunAsyncProgram()
        {
            // Settings for ADAL
            var clientId = "***************************************";
            var clientSecret = "***************************************";

            // Setting for getting blob credential from Key Vault
            var keyVault = "https://myvault.vault.azure.net";
            var keyVaultKey = "***************************************";

            // Setting for Blob
            var secretForAccountKey = "***************************************";
            string blobName = "***************************************";
            string containerName = "***************************************";


            // Acquire token 
            KeyVaultClient keyVaultClient = new KeyVaultClient(async (authority, resource, scope) =>
            {
                var adCredential = new ClientCredential(clientId, clientSecret);
                var authenticationContext = new AuthenticationContext(authority, null);
                return (await authenticationContext.AcquireTokenAsync(resource, adCredential)).AccessToken;
            });

            var retrievedSecret = await keyVaultClient.GetSecretAsync(keyVault, secretForAccountKey).ConfigureAwait(false);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== Retrieved Secret ===");
            Console.ResetColor();
            Console.WriteLine(retrievedSecret);

            // Connect to Blob
            StorageCredentials storageCredentials = new StorageCredentials(blobName, retrievedSecret.Value);
            CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(storageCredentials, useHttps: true);
            CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
            cloudBlobContainer.CreateIfNotExists();

            // Upload file with encription
            KeyVaultKeyResolver keyVaultKeyResolver = new KeyVaultKeyResolver(keyVaultClient);
            var rsa = keyVaultKeyResolver.ResolveKeyAsync(keyVault + "/keys/" + keyVaultKey, CancellationToken.None).GetAwaiter().GetResult();


            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== Key infomation ===");
            Console.ResetColor();
            Console.WriteLine(rsa.Kid);

            BlobEncryptionPolicy blobEncryptionPolicy = new BlobEncryptionPolicy(rsa, null);
            BlobRequestOptions blobRequestOptions = new BlobRequestOptions() { EncryptionPolicy = blobEncryptionPolicy };
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("Mytextfile.txt");

            string localPath = "./";
            string fileName = "quickStart" + Guid.NewGuid().ToString() + ".txt";
            string localFilePath = Path.Combine(localPath, fileName);
            await File.WriteAllTextAsync(localFilePath, "Hello world");

            using(var stream = File.OpenRead(localFilePath))
            {
                cloudBlockBlob.UploadFromStream(stream, stream.Length, null, blobRequestOptions, null);
            }
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Uploaded : " + cloudBlockBlob.Uri);
            Console.ResetColor();

            // Download and decription
            BlobEncryptionPolicy blobEncryptionPolicyForDec = new BlobEncryptionPolicy(null, keyVaultKeyResolver);
            BlobRequestOptions blobRequestOptionsForDec = new BlobRequestOptions() { EncryptionPolicy = blobEncryptionPolicyForDec };

            var downloadedfile = @"./decrypted" + Guid.NewGuid().ToString() + ".txt";
            using (var np = File.Open(downloadedfile, FileMode.Create))
            {
                cloudBlockBlob.DownloadToStream(np, null, blobRequestOptionsForDec, null);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Downloaded : " + downloadedfile);
            Console.ResetColor();

            StreamReader streamReader = new StreamReader(downloadedfile, System.Text.Encoding.GetEncoding("utf-8"));
            string output = streamReader.ReadToEnd();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Decrypted sentence...");
            Console.ResetColor();
            Console.WriteLine(output);

        }
    }
}
