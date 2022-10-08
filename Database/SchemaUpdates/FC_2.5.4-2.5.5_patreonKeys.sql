CREATE TABLE `tbl_system_patreonkeys` (
	`ID` INT(10) NOT NULL AUTO_INCREMENT,
	`AccessToken` VARCHAR(255) NOT NULL,
	`RefreshToken` VARCHAR(255) NOT NULL,
	`CreatedTimestamp` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	PRIMARY KEY (`ID`) USING BTREE
)
ENGINE=InnoDB
;