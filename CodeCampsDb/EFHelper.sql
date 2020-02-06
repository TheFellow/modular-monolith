----------------------------------------------------------------------------------------------------
-- Map Sql table to CLR DTO and types
----------------------------------------------------------------------------------------------------

declare @table varchar(128) = 'UserAccess.vUser'

; with
	excluded as (
		select major_id
			from sys.extended_properties
			where name = 'microsoft_database_tools_support'
	),
	type_map as (
		select * from (values
			('uniqueidentifier',	'Guid',		' = Guid.Empty;',	0, 0),
			('date',				'DateTime',	'',					0, 0),
			('datetime',			'DateTime',	'',					0, 0),
			('datetime2',			'DateTime',	'',					0, 0),
			('int',					'int',		'',					0, 0),
			('bigint',				'long',		'',					0, 0),
			('decimal',				'decimal',	'',					1, 1),
			('numeric',				'decimal',	'',					1, 1),
			('varchar',				'string',	' = string.Empty;',	1, 0),
			('nvarchar',			'string',	' = string.Empty;',	1, 0),
			('timestamp',			'byte[]',	' = new byte[0];',	0, 0)
		) g(sqlType, clrType, nrtDefault, hasLength, hasPrecision)
	)
select	sch.name as [Schema],
		tab.name as [Source],
		'Table' as [Type],
		col.column_id as [ColumnOrder],
		col.name as [Column],
		typ.name as [Type],
		col.max_length as [Length],
		col.precision as [Precision],
		col.scale as [Scale],
		col.is_nullable,
		map.clrType,
		map.hasLength,
		map.hasPrecision,
		'public ' + clrType + ' ' + col.name + ' { get; set; }' + nrtDefault as DtoExpr,
		CONVERT(varchar(max),
			'builder.Property(o => o.' + col.name + ')'
				+ IIF(map.sqlType = 'timestamp', '.IsRowVersion()',
					'.HasColumnName("' + col.name + '")'
					+ '.HasColumnType("' + typ.Name
						+ IIF(hasLength = 1, '(' + convert(varchar, IIF(clrType = 'string' and LEFT(sqlType, 1) = 'n', col.max_length / 2, col.max_length)), '')
						+ IIF(hasPrecision = 1, ',' + convert(varchar, col.precision), '')
						+ IIF(hasLength + hasPrecision > 0, ')', '')
					+ '")'
				+ IIF(hasLength = 1 and clrType = 'string', '.HasMaxLength(' + convert(varchar, col.max_length) + ')', '')
				+ IIF(col.is_nullable = 0, '.IsRequired()', '')
			)
			+ ';'
			) as [Builder]
	from sys.schemas sch
	inner join sys.tables tab on sch.schema_id = tab.schema_id
	inner join sys.columns col on tab.object_id = col.object_id
	inner join sys.types typ
		on col.system_type_id = typ.system_type_id
		and typ.name <> 'sysname'
	left join type_map map on typ.name = map.sqlType
	where tab.object_id not in (select major_id from excluded)
		and tab.object_id = ISNULL(object_id(@table), tab.object_id)
union all
select	sch.name as [Schema],
		tab.name as [Source],
		'View' as [Type],
		col.column_id as [ColumnOrder],
		col.name as [Column],
		typ.name as [Type],
		col.max_length as [Length],
		col.precision as [Precision],
		col.scale as [Scale],
		col.is_nullable,
		map.clrType,
		map.hasLength,
		map.hasPrecision,
		'public ' + clrType + ' ' + col.name + ' { get; set; }' + nrtDefault as DtoExpr,
		CONVERT(varchar(max),
			'builder.Property(o => o.' + col.name + ')'
				+ IIF(map.sqlType = 'timestamp', '.IsRowVersion()',
					'.HasColumnName("' + col.name + '")'
					+ '.HasColumnType("' + typ.Name
						+ IIF(hasLength = 1, '(' + convert(varchar, IIF(clrType = 'string' and LEFT(sqlType, 1) = 'n', col.max_length / 2, col.max_length)), '')
						+ IIF(hasPrecision = 1, ',' + convert(varchar, col.precision), '')
						+ IIF(hasLength + hasPrecision > 0, ')', '')
					+ '")'
			)
			+ ';'
			) as [Builder]
	from sys.schemas sch
	inner join sys.views tab on sch.schema_id = tab.schema_id
	inner join sys.columns col on tab.object_id = col.object_id
	inner join sys.types typ
		on col.system_type_id = typ.system_type_id
		and typ.name <> 'sysname'
	left join type_map map on typ.name = map.sqlType
	where tab.object_id not in (select major_id from excluded)
		and tab.object_id = ISNULL(object_id(@table), tab.object_id)
	order by sch.name,
		tab.name,
		col.column_id