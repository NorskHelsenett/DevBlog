package main

import (
	"context"
	"fmt"
	"github.com/go-openapi/runtime"
	"github.com/grafana/grafana-openapi-client-go/models"
)

func provision() {
	ctx, span := tracer.Start(context.Background(), "provision")
	defer span.End()
	for _, organization := range cfg.Organizations {
		err := provisionOrganization(ctx, organization)
		if err != nil {
			logger.ErrorContext(ctx, "error while provisioning organization", "organization", organization.Name, "error", err)
		}
	}
}

func provisionOrganization(ctx context.Context, organization models.CreateOrgCommand) error {
	ctx, span := tracer.Start(ctx, fmt.Sprintf("provision organization %s", organization.Name))
	defer span.End()
	var orgId int64
	existingOrganization, err := grafanaClient.Orgs.GetOrgByName(organization.Name, withContext(ctx))
	if err != nil {
		createdOrganization, err := grafanaClient.Orgs.CreateOrg(&organization, withContext(ctx))
		if err != nil {
			logger.ErrorContext(ctx, "error creating organization", "error", err)
			return err
		}
		orgId = *createdOrganization.Payload.OrgID
	} else {
		orgId = existingOrganization.Payload.ID
	}
	grafanaClient = grafanaClient.WithOrgID(orgId)
	provisionDataSources(ctx, organization)
	provisionFolders(ctx)
	provisionDashboards(ctx)

	return nil
}

func provisionDataSources(ctx context.Context, organization models.CreateOrgCommand) {
	ctx, span := tracer.Start(ctx, "provision data sources")
	defer span.End()
	for _, dataSource := range cfg.DataSources {
		dataSource.SecureJSONData = map[string]string{"httpHeaderValue1": organization.Name}
		_, err := grafanaClient.Datasources.GetDataSourceByUID(dataSource.UID, withContext(ctx))
		if err != nil {
			_, err = grafanaClient.Datasources.AddDataSource(&dataSource, withContext(ctx))
			if err != nil {
				logger.ErrorContext(ctx, "error creating data source", "error", err)
			}
		} else {
			_, err = grafanaClient.Datasources.UpdateDataSourceByUID(dataSource.UID, &models.UpdateDataSourceCommand{
				Access:    dataSource.Access,
				IsDefault: dataSource.IsDefault,
				JSONData:  dataSource.JSONData,
				Name:      dataSource.Name,
				Type:      dataSource.Type,
				UID:       dataSource.UID,
				URL:       dataSource.URL,
			}, withContext(ctx))
			if err != nil {
				logger.ErrorContext(ctx, "error updating data source", "error", err)
			}
		}
	}
}

func provisionFolders(ctx context.Context) {
	ctx, span := tracer.Start(ctx, "provision folders")
	defer span.End()
	for _, folder := range cfg.Folders {
		existingFolder, err := grafanaClient.Folders.GetFolderByUID(folder.UID, withContext(ctx))
		if err != nil {
			_, err = grafanaClient.Folders.CreateFolder(&folder, withContext(ctx))
			if err != nil {
				logger.ErrorContext(ctx, "error creating folder", "error", err)
			}
		} else {
			_, err = grafanaClient.Folders.UpdateFolder(folder.UID, &models.UpdateFolderCommand{
				Description: folder.Description,
				Title:       folder.Title,
				Version:     existingFolder.Payload.Version,
			}, withContext(ctx))
			if err != nil {
				logger.ErrorContext(ctx, "error updating folder", "error", err)
			}
		}
		_, err = grafanaClient.FolderPermissions.UpdateFolderPermissions(folder.UID, &models.UpdateDashboardACLCommand{
			Items: []*models.DashboardACLUpdateItem{
				{
					Permission: 4,
					Role:       "Admin",
				},
				{
					Permission: 1,
					Role:       "Editor",
				},
			},
		}, withContext(ctx))
		if err != nil {
			logger.ErrorContext(ctx, "error updating folder permissions", "error", err)
		}
	}
}

func provisionDashboards(ctx context.Context) {
	ctx, span := tracer.Start(ctx, "provision dashboards")
	defer span.End()
	for _, dashboard := range cfg.Dashboards {
		existingDashboard, err := grafanaClient.Dashboards.GetDashboardByUID(dashboard.Dashboard.(map[string]interface{})["uid"].(string), withContext(ctx))
		if err == nil {
			dashboard.Dashboard.(map[string]interface{})["version"] = existingDashboard.Payload.Dashboard.(map[string]interface{})["version"]

		}
		_, err = grafanaClient.Dashboards.PostDashboard(&dashboard, withContext(ctx))
		if err != nil {
			logger.ErrorContext(ctx, "error creating/updating dashboard", "error", err)
		}
		if dashboard.Dashboard.(map[string]interface{})["uid"] == "home" {
			_, err = grafanaClient.OrgPreferences.PatchOrgPreferences(&models.PatchPrefsCmd{HomeDashboardUID: dashboard.Dashboard.(map[string]interface{})["uid"].(string)}, withContext(ctx))
			if err != nil {
				logger.ErrorContext(ctx, "error setting home dashboard in org", "error", err)
			}
		}
	}
}

func withContext(ctx context.Context) func(operation *runtime.ClientOperation) {
	return func(operation *runtime.ClientOperation) {
		operation.Context = ctx
	}
}
