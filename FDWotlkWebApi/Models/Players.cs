using System.ComponentModel.DataAnnotations;

namespace FDWotlkWebApi.Models
{
    
    //
    // username        varchar(32)         default ''                  not null,
    // gmlevel         tinyint unsigned    default 0                   not null,
    // sessionkey      longtext                                        null,
    // v               longtext                                        null,
    // s               longtext                                        null,
    // email           text                                            null,
    // joindate        datetime            default current_timestamp() not null,
    // lockedIp        varchar(30)         default '0.0.0.0'           not null,
    // failed_logins   int(11) unsigned    default 0                   not null,
    // locked          tinyint unsigned    default 0                   not null,
    // last_module     char(32)            default ''                  null,
    // module_day      mediumint unsigned  default 0                   not null,
    // active_realm_id int(11) unsigned    default 0                   not null,
    // expansion       tinyint unsigned    default 0                   not null,
    // mutetime        bigint(40) unsigned default 0                   not null,
    // locale          varchar(4)          default ''                  not null,
    // os              varchar(4)          default '0'                 not null,
    // platform        varchar(4)          default '0'                 not null,
    // token           text                                            null,
    // flags           int unsigned        default 0                   not null,




    
    public class Player
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(32, MinimumLength = 5)]
        public string Username { get; set; } = string.Empty;

        [Range(0, 255)]
        public byte GmLevel { get; set; }

        [StringLength(255)]
        public string? Email { get; set; }

        public DateTime JoinDate { get; set; }

        [StringLength(30)] public string LastIp { get; set; } = "" ;
        // DB column `last_login` is a TIMESTAMP — map to DateTime
        public DateTime LastLogin { get; set; }
        public int FailedLogins { get; set; }
        public bool Locked { get; set; }
        public int Expansion { get; set; }
        public int ActiveRealmId { get; set; }
        
    }
    
}
