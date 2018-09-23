﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack.Redis;
using GraphView.Transaction;
using System.Threading;

namespace TransactionBenchmarkTest.TPCC
{
    class TPCCWorker
    {
        private WorkloadParam[] parameters;
        private TPCCWorkload workload;

        private int workloadCount = 0;

        //private int workloadCount;
        public int commitCount;
        public int abortCount;

        private SyncExecution execution;

        public TPCCWorker(SyncExecution execution, int workloadCount = 0)
        {
            this.commitCount = this.abortCount = 0;
            this.execution = execution;
            this.parameters = new WorkloadParam[0];
            this.workloadCount = workloadCount;
            this.workload = null;
        }
        public void SetWorkload(
            TPCCWorkload workload, WorkloadParam[] parameters)
        {
            this.workload = workload;
            this.parameters = parameters;
        }

        public void Run()
        {
            for (int i = 0; i < this.workloadCount; ++i)
            {
                WorkloadParam parameter =
                    this.parameters[i % this.parameters.Length];
                var ret = this.workload.Run(execution, parameter);
                if (ret.txFinalStatus == TxFinalStatus.COMMITTED)
                    this.commitCount++;
                else if (ret.txFinalStatus == TxFinalStatus.ABORTED)
                    this.abortCount++;
            }
        }
    }

    class TPCCBenchmark
    {
        private int workerCount;
        private int workloadCountPerWorker;

        private TPCCWorker[] tpccWorkers;

        private DateTime startTicks;
        private DateTime endTicks;

        static TPCCWorker[] InititializeWorkers(
            SyncExecutionBuilder builder, int workloadCount)
        {
            SyncExecution[] execs = builder.BuildAll();
            var workers = new TPCCWorker[execs.Length];
            for (int i = 0; i < workers.Length; ++i)
            {
                workers[i] = new TPCCWorker(execs[i], workloadCount);
            }
            return workers;
        }

        public TPCCBenchmark(
            SyncExecutionBuilder builder, int workloadCountPerWorker)
        {
            this.workloadCountPerWorker = workloadCountPerWorker;

            this.tpccWorkers = InititializeWorkers(
                builder, workloadCountPerWorker);
            this.workerCount = this.tpccWorkers.Length;
        }

        static private
        IEnumerable<string[]> ReadWorkloadFile(string filepath)
        {
            var csvReader = new System.IO.StreamReader(filepath);
            csvReader.ReadLine();    // skip csv header

            for (string line; (line = csvReader.ReadLine()) != null;)
            {
                string[] columns = line.Split(new string[] { Constants.WorkloadDelimiter }, StringSplitOptions.None);
                for (int j = 0; j < columns.Length; j++) { columns[j] = columns[j].Substring(1); }
                columns[columns.Length - 1] = columns[columns.Length - 1].Substring(0, columns[columns.Length - 1].Length - 1); // remove `"`
                yield return columns;
            }
        }

        static private List<WorkloadParam> EnumeratorTake(
            IEnumerator<WorkloadParam> iter, int n)
        {
            List<WorkloadParam> result = new List<WorkloadParam>(n);
            for (int i = 0; i < n && iter.MoveNext(); ++i)
            {
                result.Add(iter.Current);
            }
            return result;
        }

        static private Exception NotEnoughWorkload(
            int i, List<WorkloadParam> parameters, int shouldBe)
        {
            return new Exception(
                $"Not enough workload for worker {i}, " +
                $"only {parameters.Count} loaded, " +
                $"should be {shouldBe}");
        }

        public void LoadWorkload(WorkloadFactory factory, string filepath)
        {
            IEnumerator<WorkloadParam> paramIter =
                ReadWorkloadFile(filepath)
                    .Select(factory.ColumnsToParam).GetEnumerator();
            for (int i = 0; i < this.tpccWorkers.Length; ++i)
            {
                List<WorkloadParam> parameters = EnumeratorTake(
                    paramIter, this.workloadCountPerWorker);
                if (parameters.Count != this.workloadCountPerWorker)
                {
                    throw NotEnoughWorkload(
                        i, parameters, this.workloadCountPerWorker);
                }
                this.tpccWorkers[i].SetWorkload(
                    factory.NewWorkload(), parameters.ToArray());
            }
        }


        public void LoadNewOrderWorkload(string filepath)
        {
            Console.WriteLine("Loading NEW_ORDER workload...");
            LoadWorkload(new NewOrderWorkloadFactory(), filepath);
        }

        public void LoadPaymentWorkload(string filepath)
        {
            Console.WriteLine("Loading PAYMENT workload...");
            LoadWorkload(new PaymentWorkloadFactory(), filepath);
        }

        static double TotalMemoryInMB()
        {
            return GC.GetTotalMemory(false) / 1024.0 / 1024.0;
        }

        static void ForceGC()
        {
            Console.WriteLine($"Before GC: {TotalMemoryInMB():F3}MB is used");
            GC.Collect();
            Console.WriteLine($"After GC: {TotalMemoryInMB():F3}MB is used");
        }

        public void Run()
        {
            Console.WriteLine("Running TPCC workload...");
            ForceGC();
            this.startTicks = DateTime.UtcNow;

            Thread[] threads = new Thread[workerCount];
            for (int i = 0; i < this.workerCount; i++)
            {
                threads[i] = new Thread(new ThreadStart(tpccWorkers[i].Run));
                threads[i].Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            this.endTicks = DateTime.UtcNow;
        }

        public void PrintStats()
        {
            int committed = this.tpccWorkers.Select(worker => worker.commitCount).Sum();
            int aborted = this.tpccWorkers.Select(worker => worker.abortCount).Sum();
            Console.WriteLine($"Committed: {committed}, aborted: {aborted}");
            Console.WriteLine($"Abort rate: {(double)aborted / (committed + aborted):F3}");
        }

        internal int Throughput
        {
            get
            {
                double seconds = (this.endTicks - this.startTicks).TotalSeconds;
                int workloadTotal = this.workerCount * this.workloadCountPerWorker;
                Console.WriteLine("Processed {0} workloads in {1} seconds", workloadTotal, seconds);
                return (int)(workloadTotal / seconds);
            }
        }

    }
}
