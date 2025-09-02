package main

import (
	"encoding/json"
	"github.com/grafana/grafana-openapi-client-go/models"
	"os"
)

type config struct {
	Organizations []models.CreateOrgCommand     `json:"organizations"`
	DataSources   []models.AddDataSourceCommand `json:"dataSources"`
	Folders       []models.CreateFolderCommand  `json:"folders"`
	Dashboards    []models.SaveDashboardCommand `json:"dashboards"`
}

func readConfig() (*config, error) {
	configFile, err := os.ReadFile("config.json")
	if err != nil {
		return nil, err
	}

	var config config
	err = json.Unmarshal(configFile, &config)
	if err != nil {
		return nil, err
	}

	return &config, nil
}
