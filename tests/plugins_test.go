package tests

import (
	"fmt"
	"os"
	"testing"

	"github.com/rumblefrog/go-a2s"
	"github.com/stretchr/testify/assert"
)

func Getenv(key string, defaultValue string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}

	return defaultValue
}

const (
	anonymizedName = "StreamerFriendly"
)

var (
	serverHost = Getenv("SERVER_HOST", "127.0.0.1")
	serverPort = Getenv("SERVER_PORT", "28015")
	serverIP   = fmt.Sprintf("%s:%s", serverHost, serverPort)
)

// Test_StreamerFriendly is expected to be ran against a populated server with the plugin loaded
func Test_StreamerFriendly(t *testing.T) {
	client, err := a2s.NewClient(serverIP)
	if err != nil {
		panic(err)
	}

	playerInfo, err := client.QueryPlayer()
	if err != nil {
		panic(err)
	}

	t.Run("player names in server query are anonymized", func(t *testing.T) {

		if len(playerInfo.Players) == 0 {
			assert.Fail(t, "unable to test plugin due to absent player count")
		}

		for _, player := range playerInfo.Players {
			assert.Equal(t, anonymizedName, player.Name)
		}
	})
}
