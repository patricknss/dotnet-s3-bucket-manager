var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
var bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME");
var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "USEast1";

if (string.IsNullOrWhiteSpace(accessKey) ||
    string.IsNullOrWhiteSpace(secretKey) ||
    string.IsNullOrWhiteSpace(bucketName))
{
    Console.WriteLine("Configure AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_BUCKET_NAME para testar o S3.");
    Console.WriteLine("A classe BucketS3 recebe as credenciais no construtor e usa um prefixo interno sanitizado.");
    return;
}

using var bucketS3 = new BucketS3(accessKey, secretKey, region);

var arquivos = await bucketS3.ListarArquivos(bucketName);

Console.WriteLine("Arquivos encontrados no prefixo configurado:");
foreach (var arquivo in arquivos)
{
    Console.WriteLine(arquivo);
}
