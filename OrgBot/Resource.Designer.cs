﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace OrgBot {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resource {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resource() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("OrgBot.Resource", typeof(Resource).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to API rate limit exceeded. Retrying after {0} seconds..
        /// </summary>
        internal static string API_rate_limit_exceeded {
            get {
                return ResourceManager.GetString("API rate limit exceeded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Bot is now disengaged and will not process updates..
        /// </summary>
        internal static string Bot_disengaged {
            get {
                return ResourceManager.GetString("Bot disengaged", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Bot is now engaged and processing updates..
        /// </summary>
        internal static string Bot_is_engaged {
            get {
                return ResourceManager.GetString("Bot is engaged", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This chat does not belong to bot owner..
        /// </summary>
        internal static string chat_does_not_belong_owner {
            get {
                return ResourceManager.GetString("chat does not belong owner", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Disable any restricting users when deleting spam.
        /// </summary>
        internal static string Disable_any_restricting {
            get {
                return ResourceManager.GetString("Disable any restricting", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Enable banning users when deleting spam.
        /// </summary>
        internal static string Enable_banning {
            get {
                return ResourceManager.GetString("Enable banning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Enable muting users (instead of banning).
        /// </summary>
        internal static string Enable_muting {
            get {
                return ResourceManager.GetString("Enable muting", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid restriction duration specified. Please provide &apos;0&apos; for infinite or a positive integer in minutes..
        /// </summary>
        internal static string Invalid_restriction_duration {
            get {
                return ResourceManager.GetString("Invalid restriction duration", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Please use commands to interact with the bot. Use /help to see available commands..
        /// </summary>
        internal static string non_command_error {
            get {
                return ResourceManager.GetString("non-command error", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Bot is not an Admin..
        /// </summary>
        internal static string not_an_Admin {
            get {
                return ResourceManager.GetString("not an Admin", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to You are not the bot owner..
        /// </summary>
        internal static string not_bot_owner {
            get {
                return ResourceManager.GetString("not bot owner", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Stop processing updates.
        /// </summary>
        internal static string Pause {
            get {
                return ResourceManager.GetString("Pause", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Restarting the service..
        /// </summary>
        internal static string Restarting {
            get {
                return ResourceManager.GetString("Restarting", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Restriction duration set to {0} minutes..
        /// </summary>
        internal static string Restriction_duration {
            get {
                return ResourceManager.GetString("Restriction duration", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Restriction duration set to forever..
        /// </summary>
        internal static string Restriction_forever {
            get {
                return ResourceManager.GetString("Restriction forever", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Set the restriction duration in minutes or &apos;0&apos; for infinite.
        /// </summary>
        internal static string Set_the_restriction_duration {
            get {
                return ResourceManager.GetString("Set the restriction duration", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Set the spam time window in seconds.
        /// </summary>
        internal static string Set_the_spam_time {
            get {
                return ResourceManager.GetString("Set the spam time", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Show the last {0} actions.
        /// </summary>
        internal static string Show_the_last_actions {
            get {
                return ResourceManager.GetString("Show the last actions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Show available commands.
        /// </summary>
        internal static string ShowHelp {
            get {
                return ResourceManager.GetString("ShowHelp", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Bot is shutting down..
        /// </summary>
        internal static string shutting_down {
            get {
                return ResourceManager.GetString("shutting down", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Silent mode is now {0}..
        /// </summary>
        internal static string Silent_mode {
            get {
                return ResourceManager.GetString("Silent mode", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Silent mode is now enabled..
        /// </summary>
        internal static string Silent_mode_enabled {
            get {
                return ResourceManager.GetString("Silent mode enabled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Start processing updates.
        /// </summary>
        internal static string Start {
            get {
                return ResourceManager.GetString("Start", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Stop the bot.
        /// </summary>
        internal static string Stop {
            get {
                return ResourceManager.GetString("Stop", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Toggle silent mode (no messages on spam actions).
        /// </summary>
        internal static string Toggle_silent {
            get {
                return ResourceManager.GetString("Toggle silent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Too many requests: {0} seconds to wait..
        /// </summary>
        internal static string Too_many_requests {
            get {
                return ResourceManager.GetString("Too many requests", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unknown command..
        /// </summary>
        internal static string UnknownCommand {
            get {
                return ResourceManager.GetString("UnknownCommand", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Spamming users will be banned..
        /// </summary>
        internal static string users_will_be_banned {
            get {
                return ResourceManager.GetString("users will be banned", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Spamming users will be muted..
        /// </summary>
        internal static string users_will_be_muted {
            get {
                return ResourceManager.GetString("users will be muted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Spamming users will not be restricted..
        /// </summary>
        internal static string users_will_not_be_restricted {
            get {
                return ResourceManager.GetString("users will not be restricted", resourceCulture);
            }
        }
    }
}
