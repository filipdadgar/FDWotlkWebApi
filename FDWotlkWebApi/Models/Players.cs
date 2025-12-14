using System.ComponentModel.DataAnnotations;

namespace FDWotlkWebApi.Models
{
    
 
    //     id              int(11) unsigned auto_increment comment 'Identifier'
    // primary key,
    //     username        varchar(32)         default ''                    not null,
    // gmlevel         tinyint unsigned    default 0                     not null,
    // sessionkey      longtext                                          null,
    // v               longtext                                          null,
    // s               longtext                                          null,
    // email           text                                              null,
    // joindate        timestamp           default current_timestamp()   not null,
    // last_ip         varchar(30)         default '0.0.0.0'             not null,
    // failed_logins   int(11) unsigned    default 0                     not null,
    // locked          tinyint unsigned    default 0                     not null,
    // last_login      timestamp           default '0000-00-00 00:00:00' not null,
    // active_realm_id int(11) unsigned    default 0                     not null,
    // expansion       tinyint unsigned    default 0                     not null,
    // mutetime        bigint(40) unsigned default 0                     not null,
    // locale          tinyint unsigned    default 0                     not null,
    // token           text                                              null,
    // constraint idx_username
    // unique (username)

    
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
