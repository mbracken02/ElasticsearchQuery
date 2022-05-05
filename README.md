# ElasticsearchQuery
Its a simple IQueryable implementation for Elasticsearch built with Netstandard 2.0.
Implementation of translating LINQ queries to ElasticSearch with .NET 4.6.1 that uses the Nest client.  

This project borrows heavily from [ElasticLINQ](https://github.com/ElasticLINQ/ElasticLINQ) and [ElasticSearchQuery](https://github.com/leonardosimoura/ElasticsearchQuery)

## Usage
 ```csharp
 
 var client = new ElasticClient();
 var query = ElasticSearchQueryFactory.CreateQuery<Product>(client);
 
 ```
 
See the Test project for the queries currently supported.
 
## Custom Index/Type Mapping
  ```csharp
 
 ElasticQueryMapper.Map(typeof(Product), indexName, indexType);
 
 ```
 The default mapping will need to be set in the project as part of the settings.
 ```csharp
 var settings = new ConnectionSettings(node);
 settings.DefaultMappingFor<ProductTest>(m => m.IdProperty(p => p.ProductId).IndexName("producttest"));
 settings.DefaultMappingFor<ProductTestMultiple>(m => m.IdProperty(p => p.ProductId).IndexName("producttestm"));
 
 ```
 
## Suported Nest Versions

Version 0.1.5 Supports ElasticSearch 7.X

Version 0.1.4 and lower Supports ElasticSearch 6.0.0 - 6.6.0


## TODO

Add support for SUM

## Latest Work

Added support for Nested properties

Added support for searching collections within an object using LINQ Any method

## Under development

We have some improvements and implementations to do.
Any help is welcome

## More About Elasticsearch
https://www.elastic.co/guide/en/elasticsearch/reference/current/index.html
