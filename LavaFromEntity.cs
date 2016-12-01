using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Workflow;

namespace com.shepherdchurch.WorkflowActions
{
    /// <summary>
    /// Runs Lava and sets an attribute's value to the result.
    /// </summary>
    [ActionCategory( "Utility" )]
    [Description( "Loads an entity object then passes it through Lava and sets an attribute's value to the result." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Lava From Entity" )]

    [EntityTypeField( "Entity Type", "The type of entity to use when loading from the guid.", true, "", 0 )]
    [WorkflowAttribute( "Entity Attribute", "The attribute that contains the GUID value to load the Entity from.", true, "", "", 1 )]
    [WorkflowAttribute( "Attribute", "The attribute to store the result in.", true, "", "", 2 )]
    [CodeEditorField( "Lava", "The <span class='tip tip-lava'></span> to run. The entity object will be available as 'Entity'.", CodeEditorMode.Lava, CodeEditorTheme.Rock, 300, true, "", "", 3, "Value" )]
    public class LavaFromEntity : ActionComponent
    {
        /// <summary>
        /// Executes the specified workflow.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="action">The action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns></returns>
        public override bool Execute( RockContext rockContext, WorkflowAction action, Object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            //
            // Get the GUID values for the Attribute to store the value into and the Attribute that
            // contains the Entity GUID.
            //
            Guid attributeGuid = GetAttributeValue( action, "Attribute" ).AsGuid();
            Guid entityAttributeGuid = GetAttributeValue( action, "EntityAttribute" ).AsGuid();
            if ( !attributeGuid.IsEmpty() && !entityAttributeGuid.IsEmpty() )
            {
                //
                // Now get the actual Attribute objects so we know what keys to use.
                //
                var attribute = AttributeCache.Read( attributeGuid, rockContext );
                var entityAttribute = AttributeCache.Read( entityAttributeGuid, rockContext );
                if ( attribute != null && entityAttribute != null )
                {
                    Guid entityGuid = Guid.Empty;

                    //
                    // Get the entity GUID value.
                    //
                    if ( entityAttribute.EntityTypeId == new Workflow().TypeId )
                    {
                        entityGuid = action.Activity.Workflow.GetAttributeValue( entityAttribute.Key ).AsGuid();
                    }
                    else if ( entityAttribute.EntityTypeId == new WorkflowActivity().TypeId )
                    {
                        entityGuid = action.Activity.GetAttributeValue( entityAttribute.Key ).AsGuid();
                    }

                    //
                    // Find the Entity Type that we have been configured to use.
                    //
                    var entityTypeCache = EntityTypeCache.Read( GetAttributeValue( action, "EntityType" ).AsGuid() );
                    if ( entityTypeCache != null )
                    {
                        string value = GetAttributeValue( action, "Value" );
                        var mergeFields = GetMergeFields( action );

                        //
                        // Load the dynamic entity from the database and put it in the merge fields.
                        //
                        var dynamicEntity = GetEntityFromTypeByGuid( entityTypeCache.GetEntityType(), entityGuid );
                        mergeFields.Add( "Entity", dynamicEntity );

                        value = value.ResolveMergeFields( mergeFields );

                        //
                        // Store the result into the attribute.
                        //
                        if ( attribute.EntityTypeId == new Workflow().TypeId )
                        {
                            action.Activity.Workflow.SetAttributeValue( attribute.Key, value );
                            action.AddLogEntry( string.Format( "Set '{0}' attribute to '{1}'.", attribute.Name, value ) );
                        }
                        else if ( attribute.EntityTypeId == new WorkflowActivity().TypeId )
                        {
                            action.Activity.SetAttributeValue( attribute.Key, value );
                            action.AddLogEntry( string.Format( "Set '{0}' attribute to '{1}'.", attribute.Name, value ) );
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Load an Entity from the database given it's Type and unique GUID.
        /// </summary>
        /// <param name="entityType">The object type which inherits from IEntity.</param>
        /// <param name="guid">The unique GUID of this entity that will be loaded.</param>
        /// <returns>An instance of entityType or null if not found.</returns>
        object GetEntityFromTypeByGuid( Type entityType, Guid guid )
        {
            IService service = GetServiceFromEntityType( entityType );

            if ( service != null )
            {
                MethodInfo mi = service.GetType().GetMethod( "Get", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof( Guid ) }, null );

                if ( mi != null )
                {
                    try
                    {
                        return mi.Invoke( service, new object[] { guid } );
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find and return an IService subclass for the given Entity Type. This would be equivalent to returning
        /// a PersonService instance when passed a Person entity type.
        /// </summary>
        /// <param name="entityType">The IEntity type that we need an IService object for.</param>
        /// <returns>Instance of an IService subclass or null if not found.</returns>
        protected IService GetServiceFromEntityType( Type entityType )
        {
            //
            // Check all assemblies that are non-dynamic (they throw exceptions) that do not
            // begin with 'System.' or 'Microsoft.' as those will never contain IServce
            // class types.
            //
            foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies().Where( a => !a.IsDynamic && !a.FullName.StartsWith("System.") && !a.FullName.StartsWith("Microsoft.") ) )
            {
                var types = assembly.GetExportedTypes().Where( t => typeof( IService ).IsAssignableFrom( t ) );

                //
                // Loop through each public type that inherits from IService.
                //
                foreach ( var type in types )
                {
                    //
                    // Check if this type is a generic of the entityType we are concerned with.
                    //
                    if (IsTypeGenericOf(type, entityType))
                    {
                        //
                        // Find a constructor that takes a single DbContext subclass argument.
                        //
                        foreach ( var mi in type.GetConstructors() )
                        {
                            var parameters = mi.GetParameters();
                            if ( parameters.Length == 1 && typeof( DbContext ).IsAssignableFrom( parameters[0].ParameterType ) )
                            {
                                //
                                // Create an instance of the specific subclass using the default constructor and then
                                // try to create an instance of the IService type.
                                //
                                DbContext context = (DbContext)Activator.CreateInstance( parameters[0].ParameterType, null );

                                return ( IService )Activator.CreateInstance( type, new object[] { context } );
                            }
                        }

                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Check if the type is a generic of the target type. For example, type&lt;targetType&gt;.
        /// </summary>
        /// <param name="type">The generic type to be checked.</param>
        /// <param name="targetType">The type that is being wrapped by the generic type.</param>
        /// <returns>true if type is a generic of the target type otherwise false.</returns>
        bool IsTypeGenericOf( Type type, Type targetType )
        {
            if ( type.IsGenericType )
            {
                foreach ( Type arg in type.GetGenericArguments() )
                {
                    if ( arg == targetType )
                    {
                        return true;
                    }
                }
            }

            //
            // Recursively check the base type to see if it meets the criteria.
            //
            if ( type.BaseType != null )
            {
                return IsTypeGenericOf( type.BaseType, targetType );
            }

            return false;
        }
    }
}
