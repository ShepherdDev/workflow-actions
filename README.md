## Lava From Entity

This action takes an attribute that contains a GUID and loads the
entity from the database and then allows you to format that Entity
via Lava. You configure the Entity Type that is expected in the
Settings for the action. Think of this as the `Attribute Set from
Entity` action, except that you can tell it what Entity you want.

##### Configuration

* Entity Type - The `IEntity` type to load.
* Entity Attribute - The `Attribute` that holds the GUID of the
entity object to load.
* Attribute - The `Attribute` to store the results into.
* Lava - The lava text to parse.

##### Example Usage

Let's say we have an Attribute called *Member* that contains a GUID value for a
GroupMember entity and we want to generate some text that says
`Thank you Daniel for joining the Cat Lovers group.`. We
would set the `Entity Type` to GroupMember; `Entity Attribute`
to *Member*; `Attribute` to *Value* and then set our `Lava` to
`Thank you {{ Entity.Person.FirstName }} for joining the {{ 
Entity.Group.Name }} group.`

## Loop Over String

Think of this as a `for` loop in C or C#. This action takes a
string value from an Attribute and splits it into components. Each
component is then used to execute another activity in the workflow.

##### Configuration

* Source - The `Attribute` that has the string.
* Seperator - The string to use when splitting the Source string
(usually a comma).
* Execute Each - The `Activity` to activate for each iteration.
* Execute After - The `Activity` to activate at the end of all
the iterations have executed.
* Index Key - The key name of the `Attribute` that holds the
current index in the loop in the spawned Activity. (optional)
* Value Key - The key name of the `Attribute` that the element
will be placed into in the spawned Activity. (optional)

Order matters. Concurrent activities are executed in order from top
to bottom. A currently executing activity will finish execution
before another activity begins execution. This means if you want
the loop to execute activity `B` for the loop iterations and then
execute activity `C` at the end of the loop, make sure `C` comes
after `B` in the activity order; otherwise you will find your "end
of loop" activity executing before the others finish.

> Note: While your activities will *probably* be executed in
> iteration order, you should not depend on it. For example, if
> you are using this to build a list of names, do not assume that
> just because `Index` == 0 that this will be the first name.
> Check the target attribute to see if it is empty instead.

> Note: Any actions that follow this action in the same Activity
> will be executed before your looped Activities run. For this
> reason you need to use the `Execute After` activity for any
> logic you want to run at the end of the loop.

##### Example Usage

When the Action fires it split the `Source` attribute by the
contents of the `Seperator` attribute into a temporary array. For
each component of this array it will execute the Activity defined
by the `Execute Each` attribute. If the activity has an
attributed that matches the value of the `Index Key` attribute
then its value will be set to the integer number of the index of
the element in the array. If the activity has an attribute that
matches the value of the `Value Key` attribute then its value
will be set to string value of the component from the array.

Here is a simple example of a workflow with two Activities:

* `Setup`
  * Loop Over String
* `Process`
  * Attribute Set to Group Leader
  * Assign Activity from Attribute Value
  * Email Send

This will send an e-mail to the leader of the small group identified
by the GUID component of the `Source` during each iteration of
the loop.

Because Workflows execute a single activity and action at a time
some more complex usages take a little more creativity. You cannot,
for example, have the `Process` activity send a User Entry Form
action request to each group leader and expect them to all receive
an e-mail at the same time. The first group leader will receive an
e-mail and once they have filled out the form and submitted it
then execution of the next `Process` iteration begins.

Basically, even though the term `Active` is used it is better to
think of it as:
* Many activities can be pending (active), but only one is
being executed at at time and that activity must complete before
executing the next pending (active) activity. Only one activity is
truly *active* **and executing** at a time.

In short, a single workflow is a synchronous execution context.

>Note: Using this action in a workflow that is automatically
>persisted should be avoided if possible. If you have a string
>with 100 elements in it you will be spinning up 100 activities
>in this workflow. If it is persisted to the database then that
>means all 100 activities are also persisted to the database.
>
>Be nice to your server and don't persist looping workflows unless
>you absolutely must.

##### Asynchronous Usage

A more complex usage can be accomplished with multiple workflows.
For example, lets take the above example of needing to send a
User Entry Form to each small group leader and convert it into
something that will truly run asynchronously.

* Workflow `Main`
  * `Setup`
    * Loop Over String
  * `Process`
    * Activate Workflow `Send Leader Email`
* Workflow `Send Leader Email`
  * `Main`
    * Attribute Set to Group Leader
    * Assign Activity from Attribute Value
    * User Entry Form

The above usage example performs a synchronous loop. All the
actions will be completed for each iteration before the next
iteration begins. However, because we are spinning off a new
workflow, each of those workflows will begin to process as well
and they will all run concurrently. This then becomes an
asynchronous task and the `Main` workflow completes after
initiation all of the required `Send Leader Email` workflows.

You may notice, that in this case the `Main` workflow has no
knowledge of then all the `Send Leader Email` workflows have
finished. Therefore making use of the `Execute After` setting
does not make much sense. You can still use it but it will almost
definately be executed before the child workflows have finished.

## Activate Activity With Attributes

This provides the same functionality as the normal `Activate
Activity` but provides the additional functionality of being
able to map attribute values from the current activity to the one
being activated. Think of this as passing arguments to the
activity.