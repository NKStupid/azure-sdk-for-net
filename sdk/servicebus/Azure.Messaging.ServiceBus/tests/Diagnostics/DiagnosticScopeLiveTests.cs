﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core.Tests;
using Azure.Messaging.ServiceBus.Diagnostics;
using NUnit.Framework;

namespace Azure.Messaging.ServiceBus.Tests.Diagnostics
{
    [NonParallelizable]
    public class DiagnosticScopeLiveTests : ServiceBusLiveTestBase
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task SenderReceiverActivities(bool useSessions)
        {
            await using (var scope = await ServiceBusScope.CreateWithQueue(enablePartitioning: false, enableSession: useSessions))
            {
                using var listener = new TestDiagnosticListener(EntityScopeFactory.DiagnosticNamespace);
                var client = new ServiceBusClient(TestEnvironment.ServiceBusConnectionString);
                ServiceBusSender sender = client.CreateSender(scope.QueueName);
                string sessionId = null;
                if (useSessions)
                {
                    sessionId = "sessionId";
                }
                int numMessages = 5;
                var msgs = GetMessages(numMessages, sessionId);
                await sender.SendAsync(msgs);
                Activity[] sendActivities = AssertSendActivities(useSessions, sender, msgs, listener);

                ServiceBusReceiver receiver = null;
                if (useSessions)
                {
                    receiver = await client.CreateSessionReceiverAsync(scope.QueueName);
                }
                else
                {
                    receiver = client.CreateReceiver(scope.QueueName);
                }

                var remaining = numMessages;
                List<ServiceBusReceivedMessage> receivedMsgs = new List<ServiceBusReceivedMessage>();
                while (remaining > 0)
                {
                    // loop in case we don't receive all messages in one attempt
                    var received = await receiver.ReceiveBatchAsync(remaining);
                    receivedMsgs.AddRange(received);
                    (string Key, object Value, DiagnosticListener) receiveStart = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.ReceiveActivityName + ".Start", receiveStart.Key);

                    Activity receiveActivity = (Activity)receiveStart.Value;
                    AssertCommonTags(receiveActivity, receiver.EntityPath, receiver.FullyQualifiedNamespace);
                    CollectionAssert.Contains(
                        receiveActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.RequestedMessageCountAttribute,
                            remaining.ToString()));
                    CollectionAssert.Contains(
                        receiveActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.MessageIdAttribute,
                            string.Join(",", received.Select(m => m.MessageId).ToArray())));
                    remaining -= received.Count;

                    if (useSessions)
                    {
                        CollectionAssert.Contains(
                            receiveActivity.Tags,
                            new KeyValuePair<string, string>(
                                DiagnosticProperty.SessionIdAttribute,
                                string.Join(",", msgs.Select(m => m.SessionId).Distinct().ToArray())));
                    }
                    var receiveLinkedActivities = ((IEnumerable<Activity>)receiveActivity.GetType().GetProperty("Links").GetValue(receiveActivity)).ToArray();
                    for (int i = 0; i < receiveLinkedActivities.Length; i++)
                    {
                        Assert.AreEqual(sendActivities[i].ParentId, receiveLinkedActivities[i].ParentId);
                    }
                    (string Key, object Value, DiagnosticListener) receiveStop = listener.Events.Dequeue();

                    Assert.AreEqual(DiagnosticProperty.ReceiveActivityName + ".Stop", receiveStop.Key);
                }

                var msgIndex = 0;

                var completed = receivedMsgs[msgIndex];
                await receiver.CompleteAsync(completed);
                (string Key, object Value, DiagnosticListener) completeStart = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.CompleteActivityName + ".Start", completeStart.Key);
                Activity completeActivity = (Activity)completeStart.Value;
                AssertCommonTags(completeActivity, receiver.EntityPath, receiver.FullyQualifiedNamespace);
                AssertLockTokensTag(completeActivity, completed.LockToken);
                (string Key, object Value, DiagnosticListener) completeStop = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.CompleteActivityName + ".Stop", completeStop.Key);

                var deferred = receivedMsgs[++msgIndex];
                await receiver.DeferAsync(deferred);
                (string Key, object Value, DiagnosticListener) deferStart = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.DeferActivityName + ".Start", deferStart.Key);
                Activity deferActivity = (Activity)deferStart.Value;
                AssertCommonTags(deferActivity, receiver.EntityPath, receiver.FullyQualifiedNamespace);
                AssertLockTokensTag(deferActivity, deferred.LockToken);
                (string Key, object Value, DiagnosticListener) deferStop = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.DeferActivityName + ".Stop", deferStop.Key);

                var deadLettered = receivedMsgs[++msgIndex];
                await receiver.DeadLetterAsync(deadLettered);
                (string Key, object Value, DiagnosticListener) deadLetterStart = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.DeadLetterActivityName + ".Start", deadLetterStart.Key);
                Activity deadLetterActivity = (Activity)deadLetterStart.Value;
                AssertCommonTags(deadLetterActivity, receiver.EntityPath, receiver.FullyQualifiedNamespace);
                AssertLockTokensTag(deadLetterActivity, deadLettered.LockToken);
                (string Key, object Value, DiagnosticListener) deadletterStop = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.DeadLetterActivityName + ".Stop", deadletterStop.Key);

                var abandoned = receivedMsgs[++msgIndex];
                await receiver.AbandonAsync(abandoned);
                (string Key, object Value, DiagnosticListener) abandonStart = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.AbandonActivityName + ".Start", abandonStart.Key);
                Activity abandonActivity = (Activity)abandonStart.Value;
                AssertCommonTags(abandonActivity, receiver.EntityPath, receiver.FullyQualifiedNamespace);
                AssertLockTokensTag(abandonActivity, abandoned.LockToken);
                (string Key, object Value, DiagnosticListener) abandonStop = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.AbandonActivityName + ".Stop", abandonStop.Key);

                var receiveDeferMsg = await receiver.ReceiveDeferredMessageAsync(deferred.SequenceNumber);
                (string Key, object Value, DiagnosticListener) receiveDeferStart = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.ReceiveDeferredActivityName + ".Start", receiveDeferStart.Key);
                Activity receiveDeferActivity = (Activity)receiveDeferStart.Value;
                AssertCommonTags(receiveDeferActivity, receiver.EntityPath, receiver.FullyQualifiedNamespace);
                CollectionAssert.Contains(
                    receiveDeferActivity.Tags,
                    new KeyValuePair<string, string>(
                        DiagnosticProperty.MessageIdAttribute,
                        deferred.MessageId));
                CollectionAssert.Contains(
                    receiveDeferActivity.Tags,
                    new KeyValuePair<string, string>(
                        DiagnosticProperty.SequenceNumbersAttribute,
                        deferred.SequenceNumber.ToString()));
                (string Key, object Value, DiagnosticListener) receiveDeferStop = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.ReceiveDeferredActivityName + ".Stop", receiveDeferStop.Key);

                // renew lock
                if (useSessions)
                {
                    var sessionReceiver = (ServiceBusSessionReceiver)receiver;
                    await sessionReceiver.RenewSessionLockAsync();
                    (string Key, object Value, DiagnosticListener) renewStart = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.RenewSessionLockActivityName + ".Start", renewStart.Key);
                    Activity renewActivity = (Activity)renewStart.Value;
                    AssertCommonTags(renewActivity, receiver.EntityPath, receiver.FullyQualifiedNamespace);
                    CollectionAssert.Contains(
                        renewActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.SessionIdAttribute,
                            "sessionId"));

                    (string Key, object Value, DiagnosticListener) renewStop = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.RenewSessionLockActivityName + ".Stop", renewStop.Key);

                    // set state
                    var state = Encoding.UTF8.GetBytes("state");
                    await sessionReceiver.SetSessionStateAsync(state);
                    (string Key, object Value, DiagnosticListener) setStateStart = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.SetSessionStateActivityName + ".Start", setStateStart.Key);
                    Activity setStateActivity = (Activity)setStateStart.Value;
                    AssertCommonTags(setStateActivity, sessionReceiver.EntityPath, sessionReceiver.FullyQualifiedNamespace);
                    CollectionAssert.Contains(
                        setStateActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.SessionIdAttribute,
                            "sessionId"));

                    (string Key, object Value, DiagnosticListener) setStateStop = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.SetSessionStateActivityName + ".Stop", setStateStop.Key);

                    // get state
                    var getState = await sessionReceiver.GetSessionStateAsync();
                    Assert.AreEqual(state, getState);
                    (string Key, object Value, DiagnosticListener) getStateStart = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.GetSessionStateActivityName + ".Start", getStateStart.Key);
                    Activity getStateActivity = (Activity)getStateStart.Value;
                    AssertCommonTags(getStateActivity, sessionReceiver.EntityPath, sessionReceiver.FullyQualifiedNamespace);
                    CollectionAssert.Contains(
                        getStateActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.SessionIdAttribute,
                            "sessionId"));

                    (string Key, object Value, DiagnosticListener) getStateStop = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.GetSessionStateActivityName + ".Stop", getStateStop.Key);
                }
                else
                {
                    await receiver.RenewMessageLockAsync(receivedMsgs[4]);
                    (string Key, object Value, DiagnosticListener) renewStart = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.RenewMessageLockActivityName + ".Start", renewStart.Key);
                    Activity renewActivity = (Activity)renewStart.Value;
                    AssertCommonTags(renewActivity, receiver.EntityPath, receiver.FullyQualifiedNamespace);
                    AssertLockTokensTag(renewActivity, receivedMsgs[4].LockToken);
                    CollectionAssert.Contains(
                        renewActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.LockedUntilAttribute,
                            receivedMsgs[4].LockedUntil.ToString()));

                    (string Key, object Value, DiagnosticListener) renewStop = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.RenewMessageLockActivityName + ".Stop", renewStop.Key);
                }

                // schedule
                msgs = GetMessages(numMessages, sessionId);

                foreach (var msg in msgs)
                {
                    var seq = await sender.ScheduleMessageAsync(msg, DateTimeOffset.UtcNow.AddMinutes(1));
                    Assert.IsNotNull(msg.Properties[DiagnosticProperty.DiagnosticIdAttribute]);

                    (string Key, object Value, DiagnosticListener) startMessage = listener.Events.Dequeue();
                    Activity messageActivity = (Activity)startMessage.Value;
                    AssertCommonTags(messageActivity, sender.EntityPath, sender.FullyQualifiedNamespace);
                    Assert.AreEqual(DiagnosticProperty.MessageActivityName + ".Start", startMessage.Key);

                    (string Key, object Value, DiagnosticListener) stopMessage = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.MessageActivityName + ".Stop", stopMessage.Key);

                    (string Key, object Value, DiagnosticListener) startSchedule = listener.Events.Dequeue();
                    AssertCommonTags((Activity)startSchedule.Value, sender.EntityPath, sender.FullyQualifiedNamespace);

                    Assert.AreEqual(DiagnosticProperty.ScheduleActivityName + ".Start", startSchedule.Key);
                    (string Key, object Value, DiagnosticListener) stopSchedule = listener.Events.Dequeue();

                    Assert.AreEqual(DiagnosticProperty.ScheduleActivityName + ".Stop", stopSchedule.Key);
                    var linkedActivities = ((IEnumerable<Activity>)startSchedule.Value.GetType().GetProperty("Links").GetValue(startSchedule.Value)).ToArray();
                    Assert.AreEqual(1, linkedActivities.Length);
                    Assert.AreEqual(messageActivity.Id, linkedActivities[0].ParentId);

                    await sender.CancelScheduledMessageAsync(seq);
                    (string Key, object Value, DiagnosticListener) startCancel = listener.Events.Dequeue();
                    AssertCommonTags((Activity)startCancel.Value, sender.EntityPath, sender.FullyQualifiedNamespace);
                    Assert.AreEqual(DiagnosticProperty.CancelActivityName + ".Start", startCancel.Key);

                    (string Key, object Value, DiagnosticListener) stopCancel = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.CancelActivityName + ".Stop", stopCancel.Key);
                }

                // send a batch
                var batch = await sender.CreateBatchAsync();
                for (int i = 0; i < numMessages; i++)
                {
                    batch.TryAdd(GetMessage(sessionId));
                }
                await sender.SendAsync(batch);
                AssertSendActivities(useSessions, sender, batch.AsEnumerable<ServiceBusMessage>(), listener);
            };
        }

        [Test]
        public async Task ProcessorActivities()
        {
            await using (var scope = await ServiceBusScope.CreateWithQueue(enablePartitioning: false, enableSession: false))
            {
                using var listener = new TestDiagnosticListener(EntityScopeFactory.DiagnosticNamespace);
                var client = new ServiceBusClient(TestEnvironment.ServiceBusConnectionString);
                ServiceBusSender sender = client.CreateSender(scope.QueueName);
                var messageCt = 2;
                var msgs = GetMessages(messageCt);
                await sender.SendAsync(msgs);
                Activity[] sendActivities = AssertSendActivities(false, sender, msgs, listener);

                ServiceBusProcessor processor = client.CreateProcessor(scope.QueueName, new ServiceBusProcessorOptions
                {
                    AutoComplete = false,
                    MaxReceiveWaitTime = TimeSpan.FromSeconds(10),
                    MaxConcurrentCalls = 1
                });
                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                int messageProcessedCt = 0;
                processor.ProcessMessageAsync += args =>
                {
                    messageProcessedCt++;
                    if (messageProcessedCt == messageCt)
                    {
                        tcs.SetResult(true);
                    }
                    return Task.CompletedTask;
                };
                processor.ProcessErrorAsync += ExceptionHandler;
                await processor.StartProcessingAsync();
                await tcs.Task;
                await processor.StopProcessingAsync();
                for (int i = 0; i < messageCt; i++)
                {
                    (string Key, object Value, DiagnosticListener) receiveStart = listener.Events.Dequeue();
                    (string Key, object Value, DiagnosticListener) receiveStop = listener.Events.Dequeue();
                    (string Key, object Value, DiagnosticListener) processStart = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.ProcessMessageActivityName + ".Start", processStart.Key);
                    Activity processActivity = (Activity)processStart.Value;
                    AssertCommonTags(processActivity, processor.EntityPath, processor.FullyQualifiedNamespace);
                    CollectionAssert.Contains(
                        processActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.MessageIdAttribute,
                            msgs[i].MessageId));
                    (string Key, object Value, DiagnosticListener) processStop = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.ProcessMessageActivityName + ".Stop", processStop.Key);
                }
            };
        }

        [Test]
        public async Task SessionProcessorActivities()
        {
            await using (var scope = await ServiceBusScope.CreateWithQueue(enablePartitioning: false, enableSession: true))
            {
                using var listener = new TestDiagnosticListener(EntityScopeFactory.DiagnosticNamespace);
                var client = new ServiceBusClient(TestEnvironment.ServiceBusConnectionString);
                ServiceBusSender sender = client.CreateSender(scope.QueueName);
                var messageCt = 2;
                var msgs = GetMessages(messageCt, "sessionId");
                await sender.SendAsync(msgs);
                Activity[] sendActivities = AssertSendActivities(false, sender, msgs, listener);

                ServiceBusSessionProcessor processor = client.CreateSessionProcessor(scope.QueueName,
                    new ServiceBusSessionProcessorOptions
                    {
                        AutoComplete = false,
                        MaxReceiveWaitTime = TimeSpan.FromSeconds(10),
                        MaxConcurrentCalls = 1
                    });
                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                int processedMsgCt = 0;
                processor.ProcessMessageAsync += args =>
                {
                    processedMsgCt++;
                    if (processedMsgCt == messageCt)
                    {
                        tcs.SetResult(true);
                    }
                    return Task.CompletedTask;
                };
                processor.ProcessErrorAsync += ExceptionHandler;
                await processor.StartProcessingAsync();
                await tcs.Task;
                await processor.StopProcessingAsync();
                for (int i = 0; i < messageCt; i++)
                {
                    (string Key, object Value, DiagnosticListener) receiveStart = listener.Events.Dequeue();
                    (string Key, object Value, DiagnosticListener) receiveStop = listener.Events.Dequeue();
                    (string Key, object Value, DiagnosticListener) processStart = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.ProcessSessionMessageActivityName + ".Start", processStart.Key);
                    Activity processActivity = (Activity)processStart.Value;
                    AssertCommonTags(processActivity, processor.EntityPath, processor.FullyQualifiedNamespace);
                    CollectionAssert.Contains(
                        processActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.MessageIdAttribute,
                            msgs[i].MessageId));
                    CollectionAssert.Contains(
                        processActivity.Tags,
                        new KeyValuePair<string, string>(
                            DiagnosticProperty.SessionIdAttribute,
                            msgs[i].SessionId));
                    (string Key, object Value, DiagnosticListener) processStop = listener.Events.Dequeue();
                    Assert.AreEqual(DiagnosticProperty.ProcessSessionMessageActivityName + ".Stop", processStop.Key);
                }
            };
        }

        private Activity[] AssertSendActivities(bool useSessions, ServiceBusSender sender, IEnumerable<ServiceBusMessage> msgs, TestDiagnosticListener listener)
        {
            IList<Activity> messageActivities = new List<Activity>();
            foreach (var msg in msgs)
            {
                Assert.IsNotNull(msg.Properties[DiagnosticProperty.DiagnosticIdAttribute]);
                (string Key, object Value, DiagnosticListener) startMessage = listener.Events.Dequeue();
                messageActivities.Add((Activity)startMessage.Value);
                AssertCommonTags((Activity)startMessage.Value, sender.EntityPath, sender.FullyQualifiedNamespace);
                Assert.AreEqual(DiagnosticProperty.MessageActivityName + ".Start", startMessage.Key);

                (string Key, object Value, DiagnosticListener) stopMessage = listener.Events.Dequeue();
                Assert.AreEqual(DiagnosticProperty.MessageActivityName + ".Stop", stopMessage.Key);
            }

            (string Key, object Value, DiagnosticListener) startSend = listener.Events.Dequeue();
            Assert.AreEqual(DiagnosticProperty.SendActivityName + ".Start", startSend.Key);
            Activity sendActivity = (Activity)startSend.Value;
            AssertCommonTags(sendActivity, sender.EntityPath, sender.FullyQualifiedNamespace);
            CollectionAssert.Contains(
                sendActivity.Tags,
                new KeyValuePair<string, string>(
                    DiagnosticProperty.MessageIdAttribute,
                    string.Join(",", msgs.Select(m => m.MessageId).ToArray())));
            if (useSessions)
            {
                CollectionAssert.Contains(
                    sendActivity.Tags,
                    new KeyValuePair<string, string>(
                        DiagnosticProperty.SessionIdAttribute,
                        string.Join(",", msgs.Select(m => m.SessionId).Distinct().ToArray())));
            }

            (string Key, object Value, DiagnosticListener) stopSend = listener.Events.Dequeue();
            Assert.AreEqual(DiagnosticProperty.SendActivityName + ".Stop", stopSend.Key);

            var sendLinkedActivities = ((IEnumerable<Activity>)startSend.Value.GetType().GetProperty("Links").GetValue(startSend.Value)).ToArray();
            for (int i = 0; i < sendLinkedActivities.Length; i++)
            {
                Assert.AreEqual(messageActivities[i].Id, sendLinkedActivities[i].ParentId);
            }
            return sendLinkedActivities;
        }

        private void AssertLockTokensTag(Activity activity, string lockToken)
        {
            CollectionAssert.Contains(
                    activity.Tags,
                    new KeyValuePair<string, string>(
                        DiagnosticProperty.LockTokensAttribute,
                        lockToken));
        }

        private void AssertCommonTags(Activity activity, string entityName, string fullyQualifiedNamespace)
        {
            var tags = activity.Tags;
            CollectionAssert.Contains(tags, new KeyValuePair<string, string>(DiagnosticProperty.EntityAttribute, entityName));
            CollectionAssert.Contains(tags, new KeyValuePair<string, string>(DiagnosticProperty.EndpointAttribute, fullyQualifiedNamespace));
            CollectionAssert.Contains(tags, new KeyValuePair<string, string>(DiagnosticProperty.ServiceContextAttribute, DiagnosticProperty.ServiceBusServiceContext));
        }
    }
}
