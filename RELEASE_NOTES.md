#### 0.1.0 February 14 2019 ####
First release of Akka.Persistence.Extras. Introduces the [`DeDuplicatingReceiveActor` base class](https://devops.petabridge.com/api/Akka.Persistence.Extras.DeDuplicatingReceiveActor.html), which can be used to automatically strip out duplicates and guarantee that messages are processed exactly once by the actor.

You can read more about [how to use the `DeDuplicatingReceiveActor` with Akka.NET and other `AtLeastOnceDeliveryActor` instances here](https://devops.petabridge.com/articles/msgdelivery/deduplication.html).