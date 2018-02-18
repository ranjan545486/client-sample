using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CSharpPractice
{
    // https://stackoverflow.com/questions/45983980/how-is-this-parallel-for-not-processing-all-elements
    // another better collections 
    // https://msdn.microsoft.com/en-us/library/dd267312(v=vs.110).aspx

    public class Program
    {

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler { Proxy = null, UseProxy = false }){MaxResponseContentBufferSize = 1000000};
        static int counter = 0;
        public async static Task Main(string[] args)
        {
            //by default .net will allow only 2 connections at one time.
            ServicePointManager.DefaultConnectionLimit = 10000;
            //var query =
            // from b in Enumerable.Range(1, 20).Batch(3)
            // select string.Join(", ", b);
            //Array.ForEach(query.ToArray(), Console.WriteLine);
           
            List<int> lst = new List<int>();
            //for (int i = 0; i < 200; i++){
            //   if(i%2 ==0)
            //    lst.Add(i);
            //}

            Console.WriteLine(DateTime.Now);
            ConcurrentQueue<int> queue = new ConcurrentQueue<int>();
            for (int i = 0; i < 10000; i++)
            {
               queue.Enqueue(i);
            }

            var queueQuery = queue.AsParallel().Partition(25);

          
            //var taskList = new List<Task<Uri>>();


            // works fine           
            //foreach(var b in queueQuery){

            //    Data d = new Data();
            //    var client = new HttpClient();
            //    d.productIds = b.ToList();
            //   // taskList.Add(GetResponseAsync(d));
            //    await CreateProductAsync(client, d);
            //    b.ToList().ForEach(o=> Console.WriteLine(o));
            //}

      //works partially
            try{
               await Task.WhenAll(queueQuery.Select(i => FireQuery(i)));
               

            }catch (Exception)
            {
                _httpClient.CancelPendingRequests();
            }

            foreach(var b in queueQuery){
                b.ToList().ForEach(o=>Console.WriteLine(o));
            }


            Console.WriteLine("the total queue items processed in this batch is ");


            // works with errors
            //foreach(var b in queueQuery){
            //    Data d = new Data();
            //    HttpClient client = new HttpClient();
            //    d.productIds = b.ToList();
            //    taskList.Add(GetResponseAsync(d));
            //}

            //try
            //{
            //    await Task.WhenAll(taskList.ToArray());
            //}
            //catch (Exception)
            //{
                
            //    return;
            //}

            Console.WriteLine(counter);

            Console.WriteLine(DateTime.Now);
            queue.Clear();
       


        }

        private async Task MakeCall(IEnumerable<int> i)
        {
            Data d = new Data();
            d.productIds = i.ToList();
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5000/api/products",d);
           
        }

        private async static Task<string> FireQuery2(IEnumerable<int> i, TimeSpan timeout)
        {

            Data d = new Data();
            d.productIds = i.ToList();
            using (var cts = new CancellationTokenSource(timeout))
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                   "http://localhost:5000/api/products", d, cts.Token);
                response.EnsureSuccessStatusCode();
                counter = counter + 1;

                // return URI of the created resource.
                return await response.Content.ReadAsStringAsync();
            }
        }


        private async static Task<Uri> FireQuery(IEnumerable<int> i)
        {
            
            Data d = new Data();
            d.productIds = i.ToList();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
               "http://localhost:5000/api/products", d);
            response.EnsureSuccessStatusCode();
                counter = counter + 1;
           
            // return URI of the created resource.
            return response.Headers.Location;
        }

        //private static Task RunParallelThrottled()
        //{
        //    var throtter = new ActionBlock<int>(i => CallSomeWebsite(queueQuery),
        //        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 20 });

        //    for (var i = 0; i < 300; i++)
        //    {
        //        throtter.Post(i);
        //    }
        //    throtter.Complete();
        //    return throtter.Completion;
        //}

        //private static async Task<Uri> CallSomeWebsite()
        //{
        //    var watch = Stopwatch.StartNew();
        //    var result = await client.PostAsJsonAsync("http://localhost:5000/api/products", d).ConfigureAwait(false) ;
        //    result.EnsureSuccessStatusCode();

        //    // return URI of the created resource.
        //    return result.Headers.Location;
        //}

        public async Task<List<string>> DownloadAsync(List<Uri> urls, int maxDownloads)
        {
            var concurrentQueue = new ConcurrentQueue<string>();

            using (var semaphore = new SemaphoreSlim(maxDownloads))
            using (var httpClient = new HttpClient())
            {
                var tasks = urls.Select(async (url) =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var data = await httpClient.GetStringAsync(url);
                        concurrentQueue.Enqueue(data);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks.ToArray());
            }
            return concurrentQueue.ToList();
        }

        public static async Task<JObject> GetResponseAsync(Data d)
        {
            // no Task.Run here!
            var httpClient = new HttpClient();
            //httpClient.Timeout = TimeSpan.FromMilliseconds(5);
           
            var response = await httpClient.PostAsJsonAsync(
                "http://localhost:5000/api/products", d);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<JObject>();
        }

        static async Task<Uri> CreateProductAsync(Data d)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "http://localhost:5000/api/products", d);
            response.EnsureSuccessStatusCode();

            // return URI of the created resource.
            return response.Headers.Location;
        }

       

    }
}
