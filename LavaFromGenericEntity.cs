﻿using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Workflow;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reflection;

namespace com.shepherdchurch.WorkflowActions
{
    /// <summary>
    /// Runs Lava and sets an attribute's value to the result.
    /// </summary>
    [ActionCategory( "Shepherd Church" )]
    [Description( "Loads an entity object then passes it through Lava and sets an attribute's value to the result." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Lava From Generic Entity" )]

    [EntityTypeField( "Entity Type", "The type of entity to use when loading from the guid. Tip: A Person attribute stores it's value as a Person Alias.", true, "", 0 )]
    [WorkflowAttribute( "Entity Attribute", "The attribute that contains the GUID value to load the Entity from.", true, "", "", 1 )]
    [WorkflowAttribute( "Attribute", "The attribute to store the result in.", true, "", "", 2 )]
    [CodeEditorField( "Lava", "The <span class='tip tip-lava'></span> to run. The entity object will be available as 'Entity'.", CodeEditorMode.Lava, CodeEditorTheme.Rock, 300, true, "", "", 3, "Value" )]
    public class LavaFromGenericEntity : ActionComponent
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
                var attribute = AttributeCache.Get( attributeGuid, rockContext );
                var entityAttribute = AttributeCache.Get( entityAttributeGuid, rockContext );
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
                    var entityTypeCache = EntityTypeCache.Get( GetAttributeValue( action, "EntityType" ).AsGuid() );
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
                        SetWorkflowAttributeValue( action, attributeGuid, value );
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
            System.Data.Entity.DbContext context = Rock.Reflection.GetDbContextForEntityType( entityType );
            IService service = Rock.Reflection.GetServiceForEntityType( entityType, context );

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
    }
}
