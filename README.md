# POC Bucket S3

Projeto Console Application em C# para demonstrar operacoes basicas com Amazon S3 usando o SDK oficial da AWS.

## Objetivo

A POC implementa uma classe chamada `BucketS3`, responsavel por centralizar operacoes de upload, download, listagem, exclusao e geracao de URLs assinadas para objetos em um bucket S3.

O projeto usa internamente um prefixo demonstrativo:

```text
prefixo-sanitizado-0001/
```

Todas as chaves informadas sem prefixo sao automaticamente direcionadas para essa pasta. Em um ambiente real, substitua esse valor pelo prefixo permitido para o usuario ou aplicacao.

## Tecnologias

- C#
- .NET
- Console Application
- AWS SDK for .NET
- Amazon S3

## Metodos Disponiveis

- `GerarUrlAssinada`: gera uma URL temporaria para download/visualizacao.
- `GerarUrlAssinadaUpload`: gera uma URL temporaria para upload via PUT.
- `EnviarArquivoPut`: envia arquivo diretamente ao S3 usando o SDK.
- `EnviarArquivoHttp`: envia arquivo via HTTP usando uma URL assinada.
- `BaixarArquivo`: baixa um objeto do S3 usando o SDK.
- `ListarArquivos`: lista objetos do bucket dentro do prefixo configurado.
- `DeletarArquivo`: remove um objeto do S3.

## Configuracao

As credenciais nao devem ser salvas no codigo. Para executar localmente, configure variaveis de ambiente:

```powershell
$env:AWS_ACCESS_KEY_ID="<access-key-obfuscada>"
$env:AWS_SECRET_ACCESS_KEY="<secret-key-obfuscada>"
$env:AWS_REGION="USEast1"
$env:AWS_BUCKET_NAME="<bucket-obfuscado>"
```

## Como Executar

Na raiz do projeto:

```powershell
dotnet restore buckets3\buckets3.csproj
dotnet run --project buckets3\buckets3.csproj
```

## Observacao de Seguranca

Este repositorio nao deve conter credenciais, nomes de buckets reais sensiveis ou arquivos privados. Use variaveis de ambiente ou mecanismos seguros de gerenciamento de secrets.
