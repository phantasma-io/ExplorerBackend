-- ANAKA contract duplicate
update "Events" set "ContractId"=68 where "ContractId"=67;
update "Tokens" set "ContractId"=68 where "ContractId"=67;
update "Nfts" set "ContractId"=68 where "ContractId"=67;
update "Serieses" set "ContractId"=68 where "ContractId"=67;
update "ContractMethods" set "ContractId"=68 where "ContractId"=67;
delete from "Contracts" where "ID"=67

-- TAG contract duplicate
update "Events" set "ContractId"=63 where "ContractId"=62;
update "Tokens" set "ContractId"=63 where "ContractId"=62;
update "Nfts" set "ContractId"=63 where "ContractId"=62;
update "Serieses" set "ContractId"=63 where "ContractId"=62;
update "ContractMethods" set "ContractId"=63 where "ContractId"=62;
delete from "Contracts" where "ID"=62

-- TACAL contract duplicate
update "Events" set "ContractId"=66 where "ContractId"=65;
update "Tokens" set "ContractId"=66 where "ContractId"=65;
update "Nfts" set "ContractId"=66 where "ContractId"=65;
update "Serieses" set "ContractId"=66 where "ContractId"=65;
update "ContractMethods" set "ContractId"=66 where "ContractId"=65;
delete from "Contracts" where "ID"=65
