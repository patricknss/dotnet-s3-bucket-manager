using System.Net;
using System.Net.Http.Headers;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

public sealed class BucketS3 : IDisposable
{
    private const string PastaPadrao = "prefixo-sanitizado-0001";
    private const string PrefixoPadrao = PastaPadrao + "/";

    private readonly AmazonS3Client _s3Client;
    private readonly HttpClient _httpClient;

    public BucketS3(string accessKey, string secretKey, string region)
    {
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            throw new ArgumentException("AccessKey deve ser informada.", nameof(accessKey));
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentException("SecretKey deve ser informada.", nameof(secretKey));
        }

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        _s3Client = new AmazonS3Client(credentials, ResolverRegiao(region));
        _httpClient = new HttpClient();
    }

    public string GerarUrlAssinada(string bucketName, string objectKey, TimeSpan validade)
    {
        ValidarValidade(validade);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = ValidarBucket(bucketName),
            Key = PrepararChaveNaPastaPadrao(objectKey),
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(validade)
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public string GerarUrlAssinadaUpload(
        string bucketName,
        string objectKey,
        TimeSpan validade,
        string? contentType = null)
    {
        ValidarValidade(validade);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = ValidarBucket(bucketName),
            Key = PrepararChaveNaPastaPadrao(objectKey),
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(validade),
            ContentType = contentType
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public async Task<bool> EnviarArquivoPut(
        string bucketName,
        string objectKey,
        string caminhoArquivo,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(caminhoArquivo))
        {
            throw new FileNotFoundException("Arquivo para upload nao encontrado.", caminhoArquivo);
        }

        await using var fileStream = File.OpenRead(caminhoArquivo);

        var request = new PutObjectRequest
        {
            BucketName = ValidarBucket(bucketName),
            Key = PrepararChaveNaPastaPadrao(objectKey),
            InputStream = fileStream,
            AutoCloseStream = false,
            ContentType = contentType
        };

        var response = await _s3Client.PutObjectAsync(request, cancellationToken);
        return response.HttpStatusCode is HttpStatusCode.OK;
    }

    public async Task<bool> EnviarArquivoHttp(
        string bucketName,
        string objectKey,
        string caminhoArquivo,
        TimeSpan validade,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(caminhoArquivo))
        {
            throw new FileNotFoundException("Arquivo para upload nao encontrado.", caminhoArquivo);
        }

        var urlAssinada = GerarUrlAssinadaUpload(bucketName, objectKey, validade, contentType);

        await using var fileStream = File.OpenRead(caminhoArquivo);
        using var content = new StreamContent(fileStream);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        using var response = await _httpClient.PutAsync(urlAssinada, content, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<byte[]> BaixarArquivo(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = ValidarBucket(bucketName),
            Key = PrepararChaveNaPastaPadrao(objectKey)
        };

        using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
        await using var responseStream = response.ResponseStream;
        using var memoryStream = new MemoryStream();

        await responseStream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    public async Task BaixarArquivo(
        string bucketName,
        string objectKey,
        string caminhoDestino,
        CancellationToken cancellationToken = default)
    {
        var conteudo = await BaixarArquivo(bucketName, objectKey, cancellationToken);
        var pastaDestino = Path.GetDirectoryName(caminhoDestino);

        if (!string.IsNullOrWhiteSpace(pastaDestino))
        {
            Directory.CreateDirectory(pastaDestino);
        }

        await File.WriteAllBytesAsync(caminhoDestino, conteudo, cancellationToken);
    }

    public async Task<List<string>> ListarArquivos(
        string bucketName,
        string? prefixo = null,
        IEnumerable<string>? extensoesPermitidas = null,
        CancellationToken cancellationToken = default)
    {
        var chaves = new List<string>();
        var extensoes = NormalizarExtensoes(extensoesPermitidas);
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = ValidarBucket(bucketName),
                Prefix = PrepararPrefixoNaPastaPadrao(prefixo),
                ContinuationToken = continuationToken
            };

            var response = await _s3Client.ListObjectsV2Async(request, cancellationToken);

            chaves.AddRange(response.S3Objects
                .Where(item => !item.Key.EndsWith("/", StringComparison.Ordinal))
                .Select(item => item.Key)
                .Where(chave => ExtensaoPermitida(chave, extensoes)));

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return chaves;
    }

    public async Task<bool> DeletarArquivo(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = ValidarBucket(bucketName),
            Key = PrepararChaveNaPastaPadrao(objectKey)
        };

        var response = await _s3Client.DeleteObjectAsync(request, cancellationToken);
        return response.HttpStatusCode is HttpStatusCode.NoContent;
    }

    public void Dispose()
    {
        _s3Client.Dispose();
        _httpClient.Dispose();
    }

    private static RegionEndpoint ResolverRegiao(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            throw new ArgumentException("Region deve ser informada.", nameof(region));
        }

        return region.Equals("USEast1", StringComparison.OrdinalIgnoreCase)
            ? RegionEndpoint.USEast1
            : RegionEndpoint.GetBySystemName(region);
    }

    private static string ValidarBucket(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            throw new ArgumentException("BucketName deve ser informado.", nameof(bucketName));
        }

        return bucketName.Trim();
    }

    private static string PrepararChaveNaPastaPadrao(string objectKey)
    {
        var chave = NormalizarCaminho(objectKey, nameof(objectKey));

        if (EstaNaPasta(chave, PastaPadrao))
        {
            return chave.Equals(PastaPadrao, StringComparison.OrdinalIgnoreCase) ? PrefixoPadrao : chave;
        }

        return PrefixoPadrao + chave;
    }

    private static string PrepararPrefixoNaPastaPadrao(string? prefixo)
    {
        if (string.IsNullOrWhiteSpace(prefixo))
        {
            return PrefixoPadrao;
        }

        var prefixoNormalizado = NormalizarCaminho(prefixo, nameof(prefixo));

        if (EstaNaPasta(prefixoNormalizado, PastaPadrao))
        {
            return prefixoNormalizado.Equals(PastaPadrao, StringComparison.OrdinalIgnoreCase)
                ? PrefixoPadrao
                : prefixoNormalizado;
        }

        return PrefixoPadrao + prefixoNormalizado;
    }

    private static string NormalizarCaminho(string caminho, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            throw new ArgumentException("Caminho do objeto deve ser informado.", parameterName);
        }

        return caminho.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static bool EstaNaPasta(string caminho, string pasta)
    {
        return caminho.Equals(pasta, StringComparison.OrdinalIgnoreCase) ||
            caminho.StartsWith(pasta + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidarValidade(TimeSpan validade)
    {
        if (validade <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(validade), "Validade deve ser maior que zero.");
        }
    }

    private static HashSet<string>? NormalizarExtensoes(IEnumerable<string>? extensoesPermitidas)
    {
        var extensoes = extensoesPermitidas?
            .Where(extensao => !string.IsNullOrWhiteSpace(extensao))
            .Select(extensao => extensao.Trim().StartsWith('.')
                ? extensao.Trim()
                : "." + extensao.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return extensoes is { Count: > 0 } ? extensoes : null;
    }

    private static bool ExtensaoPermitida(string chave, IReadOnlySet<string>? extensoesPermitidas)
    {
        return extensoesPermitidas is null || extensoesPermitidas.Contains(Path.GetExtension(chave));
    }
}
