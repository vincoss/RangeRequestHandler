USE [UploaderDb]
GO
/****** Object:  Table [dbo].[Streams]    Script Date: 05/21/2011 00:03:08 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Streams](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[FileId] [int] NOT NULL,
	[Partition] [tinyint] NULL,
	[Length] [bigint] NULL,
	[BlobData] [varbinary](max) NULL,
 CONSTRAINT [PK_VersionData] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
/****** Object:  Table [dbo].[Files]    Script Date: 05/21/2011 00:03:08 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Files](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](128) NOT NULL,
	[ContentType] [nvarchar](64) NULL,
	[Extension] [nvarchar](64) NULL,
	[Length] [bigint] NOT NULL,
	[DateModified] [date] NULL,
 CONSTRAINT [PK_Files] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  StoredProcedure [dbo].[streams_GetByFileId]    Script Date: 05/21/2011 00:03:13 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[streams_GetByFileId]
@FileId	int

AS

SELECT Length, BlobData FROM Streams WHERE FileId = @FileId

Return(0);
GO
/****** Object:  StoredProcedure [dbo].[streams_DeleteByFileId]    Script Date: 05/21/2011 00:03:13 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[streams_DeleteByFileId]

@FileId int
AS

DELETE FROM Streams WHERE FileId = @FileId

Return(0);
GO
/****** Object:  StoredProcedure [dbo].[Insert_Stream]    Script Date: 05/21/2011 00:03:13 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[Insert_Stream]
@FileId		int,
@Partition	tinyint,
@Length		bigint,
@BlobData   varbinary(MAX)
AS
INSERT INTO Streams (FileId, Partition, Length, BlobData)

VALUES

(@FileId, @Partition, @Length, @BlobData)

Return(0);
GO
/****** Object:  StoredProcedure [dbo].[Insert_File]    Script Date: 05/21/2011 00:03:13 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[Insert_File]
@Name				nvarchar(128),
@ContentType		nvarchar(64),
@Extension			nvarchar(64),
@Length				bigint
AS
INSERT INTO Files (Name, ContentType, Extension, Length)

VALUES

(@Name, @ContentType, @Extension, @Length)

SELECT Id FROM Files WHERE Id = SCOPE_IDENTITY()
GO
/****** Object:  StoredProcedure [dbo].[files_GetByName]    Script Date: 05/21/2011 00:03:13 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[files_GetByName]

@Name nvarchar(128)

AS

SELECT Id, Name, ContentType, Extension, Length,DateModified FROM Files WHERE Name = @Name

Return(0);
GO
