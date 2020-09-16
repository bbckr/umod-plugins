package tests

import (
	"os"
	"testing"

	"github.com/rumblefrog/go-a2s"
	"github.com/stretchr/testify/assert"
)

var (
	CONNECT_IP      = os.Getenv("CONNECT_IP")
	ANONYMIZED_NAME = "StreamerFriendly"
)

func Test_StreamerFriendly(t *testing.T) {
	client, err := a2s.NewClient(CONNECT_IP)
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
			assert.Equal(t, ANONYMIZED_NAME, player.Name)
		}
	})
}
